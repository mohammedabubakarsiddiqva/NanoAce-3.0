using BU67833_NEW.DutControl;
using DDC.Mil1553.Emace;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System;
using System.Linq;
using System.Security.Policy;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU_67833LC.DutControl
{
    public static class InputWireCheckModule
    {
        // Register Constants
        private const ushort CONFIG_1 = 0x01;
        private const ushort START_RESET = 0x03;
        private const ushort CONFIG_6 = 0x18;
        private const ushort BIT_STATUS = 0x1C;
        private const ushort FLASH_STATUS = 0x31;

        public static void ExecuteInputWireAndTristate(ISemiconductorModuleContext tsmContext ,string patternPinsOrPinGroup)
        {


            foreach (ISemiconductorModuleContext semiconductorModuleContext in tsmContext.GetSiteSemiconductorModuleContexts())
            {
                int siteNumber = semiconductorModuleContext.SiteNumbers.FirstOrDefault<int>();

                //Apply the relay Configuration to choose the current site

                //PowerUp the DUT for the current site
                DutPowerUpSequence(semiconductorModuleContext);

                //Creating a session Manager 
                var sessionManager = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session bundle using the session Manager for digital Pattern Pins
                DigitalSessionsBundle digitalSessionsBundle = sessionManager.Digital(patternPinsOrPinGroup);


                Utilities.PreciseWait(0.01);
                // Define test configuration expectation locally inside the method (Change to false if testing a 4K part)
                bool is32kExpected = true;

                // Hardware Initialization 
                //tsmContext.ControlRelay(new[] { "RC65", "RC62" }, RelayDriverAction.CloseRelay); // RC65 EEPROM to Vcc connection , RC62 for connecting SDA and SCL lines of both NanoAce and EEPROM
                //tsmContext.ControlRelay(new[] { "RC63", "RC64" }, RelayDriverAction.OpenRelay);  // RC63 and RC64 is used to switch the SDA and SCL lines between Vcc and GND
                Utilities.PreciseWait(0.05); // 50ms stabilization

                digitalSessionsBundle.BurstPattern("Reset"); // Reset Nano-ACE
                Utilities.PreciseWait(0.15); // 150ms post-reset wait

                // Protocol Self-Test 

                Utilities.PreciseWait(0.02);
                WriteRegister(semiconductorModuleContext, ConfigurationRegister1, 0x0400); // Clear Self-Test
                WriteRegister(semiconductorModuleContext, StartResetRegister, 0x0080); // Start Protocol BIT
                Utilities.PreciseWait(0.1);                  // 100ms delay
                var protocolResult = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);

                ushort siteProtocolResult = protocolResult.GetValue(siteNumber);

                // Verify Bit 15=1 (Complete), Bit 14=0 (Idle), Bit 13=1 (Passed)
                ushort protoExpectedMask = 0xE000;
                ushort protoExpectedVal = 0xA000;
                if ((siteProtocolResult & protoExpectedMask) != protoExpectedVal)
                {

                    Console.WriteLine($"[ERROR] Protocol Self-Test Failed! Read: 0x{protocolResult:X4}, Expected Mask Match: 0x{protoExpectedVal:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteProtocolResult, "protocolSelfTest");

                // RAM Self-Test

                WriteRegister(semiconductorModuleContext, StartResetRegister, 0x0400); // Clear Self-Test
                WriteRegister(semiconductorModuleContext, StartResetRegister, 0x0200); // Start RAM BIT
                Utilities.PreciseWait(0.1);                  // 100ms
                var ramResult = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort siteramResult = protocolResult.GetValue(siteNumber);

                // Verify: Bit 7=1 (Complete), Bit 6=0 (Idle), Bit 5=1 (Passed)
                ushort ramExpectedMask = 0x00E0;
                ushort ramExpectedVal = 0x00A0;
                if ((siteramResult & ramExpectedMask) != ramExpectedVal)
                {
                    Console.WriteLine($"[ERROR] RAM Self-Test Failed! Read: 0x{ramResult:X4}, Expected Mask Match: 0x{ramExpectedVal:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteramResult, "ramSelfTest");
               
                // Model & Auto-Init Verification 
                //tsmContext.ControlRelay(new[] { "RC65", "RC62" }, RelayDriverAction.CloseRelay);
                //tsmContext.ControlRelay(new[] { "RC63", "RC64" }, RelayDriverAction.OpenRelay);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(0.2); // 200ms auto-init completion
                var modelStatus = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort sitemodelStatus = protocolResult.GetValue(siteNumber);


                // Extract Model ID Bits 3-0
                ushort modelIdBits = (ushort)(sitemodelStatus & 0x000F);
                semiconductorModuleContext.PublishSingleSiteResult<double>(modelIdBits, "modelIdBits");

                // Check if 32k is expected vs 4k part read (0x0009), or vice-versa
                if (is32kExpected)
                {
                    if (modelIdBits == 0x0009)
                    {
                        Console.WriteLine($"[ERROR] Device Model Mismatch! Tester configuration expects 32K part (0x000F), but read 0x0009 (4K part). Test Failed.");
                    }
                    else if (modelIdBits != 0x000F)
                    {
                        Console.WriteLine($"[ERROR] Unexpected Model ID Read: 0x{modelIdBits:X4}. Expected 32K part (0x000F).");
                    }
                }
                else
                {
                    if (modelIdBits == 0x000F)
                    {
                        Console.WriteLine($"[ERROR] Device Model Mismatch! Tester configuration expects 4K part (0x0009), but read 0x000F (32K part). Test Failed.");
                    }
                    else if (modelIdBits != 0x0009)
                    {
                        Console.WriteLine($"[ERROR] Unexpected Model ID Read: 0x{modelIdBits:X4}. Expected 4K part (0x0009).");
                    }
                }

                // Check Auto-Init: Bit 11=1 (Complete), Bit 9=1 (Passed)
                ushort autoInitMask = 0x0A00;
                ushort autoInitVal = 0x0A00;
                if ((sitemodelStatus & autoInitMask) != autoInitVal)
                {
                    Console.WriteLine($"[ERROR] Auto-Initialization Complete/Pass Bits Mismatch! Read: 0x{modelStatus:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>((sitemodelStatus & autoInitMask), "autoIniitializationComplete");

                // FAULT INJECTION TESTS 

                // Fault Case 1: SCL and SDA Grounded
                //tsmContext.ControlRelay(new[] { "RC62" }, RelayDriverAction.OpenRelay);
                //tsmContext.ControlRelay(new[] { "RC63", "RC64" }, RelayDriverAction.CloseRelay);
                Utilities.PreciseWait(0.02);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(0.2);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0004); // Enable 64-word space
                var f1_data1 = ReadRegister(semiconductorModuleContext, ConfigurationRegister1);

                ushort siteF1_data1 = protocolResult.GetValue(siteNumber);
                var f1_flash = ReadRegister(semiconductorModuleContext, I2CEEPROMControlStatusRegister);
                ushort siteF1_flash = protocolResult.GetValue(siteNumber);
                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0000); // Revert to 32-word

                ushort expectedFlashFault = 0x1008;

                if (siteF1_flash != expectedFlashFault)
                {
                    Console.WriteLine($"[ERROR] Fault Case 1 (SCL/SDA Grounded) Flash Status Mismatch! Read: 0x{f1_flash:X4}, Expected: 0x{expectedFlashFault:X4}, Config1 Read: 0x{f1_data1:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteF1_flash, "flashStatusClkGndDataGnd");
                var autoConfig1 = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort siteAutoConfig1 = autoConfig1.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteAutoConfig1, "autoStatusClkGndDataGnd");

                // Fault Case 2: SCL Grounded, SDA to Vdd
                //tsmContext.ControlRelay(new[] { "RC63" }, RelayDriverAction.CloseRelay);
                //tsmContext.ControlRelay(new[] { "RC64" }, RelayDriverAction.OpenRelay);
                Utilities.PreciseWait(0.02);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(0.2);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0004);
                var f2_data1 = ReadRegister(semiconductorModuleContext, ConfigurationRegister1);
                ushort siteF2_data1 = protocolResult.GetValue(siteNumber);

                var f2_flash = ReadRegister(semiconductorModuleContext, I2CEEPROMControlStatusRegister);
                ushort siteF2_flash = protocolResult.GetValue(siteNumber);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0000);

                ushort expectedFlashFault2 = 0x1008;
                if (siteF2_flash != expectedFlashFault2)
                {
                    Console.WriteLine($"[ERROR] Fault Case 2 Flash Status Mismatch! Read: 0x{f2_flash:X4}, Expected: 0x{expectedFlashFault2:X4}, Config1 Read: 0x{f2_data1:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteF2_flash, "flashStatusClkGndDataVdd");
                var autoConfig2 = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort siteAutoConfig2 = autoConfig2.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteAutoConfig2, "autoStatusClkGndDataVdd");

                // Fault Case 3: SCL to Vdd, SDA Grounded
                //tsmContext.ControlRelay(new[] { "RC63" }, RelayDriverAction.OpenRelay);
                //tsmContext.ControlRelay(new[] { "RC64" }, RelayDriverAction.CloseRelay);
                Utilities.PreciseWait(0.02);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(0.2);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0004);
                var f3_data1 = ReadRegister(semiconductorModuleContext, ConfigurationRegister1);
                ushort siteF3_data1 = protocolResult.GetValue(siteNumber);

                var f3_flash = ReadRegister(semiconductorModuleContext, I2CEEPROMControlStatusRegister);
                ushort siteF3_flash = protocolResult.GetValue(siteNumber);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0000);

                ushort expectedFlashFault3 = 0x2008;
                if (siteF3_flash != expectedFlashFault3)
                {
                    Console.WriteLine($"[ERROR] Fault Case 3 Flash Status Mismatch! Read: 0x{f3_flash:X4}, Expected: 0x{expectedFlashFault3:X4}, Config1 Read: 0x{f3_data1:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteF3_flash, "flashStatusClkVddDataGnd");
                var autoConfig3 = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort siteAutoConfig3 = autoConfig2.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteAutoConfig3, "autoStatusClkVddDataGnd");
                // Fault Case 4: SCL and SDA Vdd
                //tsmContext.ControlRelay(new[] { "RC63", "RC64" }, RelayDriverAction.CloseRelay);
                Utilities.PreciseWait(0.02);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(0.2);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0004);
                var f4_data1 = ReadRegister(semiconductorModuleContext, ConfigurationRegister1);
                ushort siteF4_data1 = protocolResult.GetValue(siteNumber);

                var f4_flash = ReadRegister(semiconductorModuleContext, I2CEEPROMControlStatusRegister);
                ushort siteF4_flash = protocolResult.GetValue(siteNumber);

                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0000);

                ushort expectedFlashFault4 = 0x3008;
                if (siteF4_flash != expectedFlashFault4)
                {
                    Console.WriteLine($"[ERROR] Fault Case 4 Flash Status Mismatch! Read: 0x{f4_flash:X4}, Expected: 0x{expectedFlashFault4:X4}, Config1 Read: 0x{f4_data1:X4}");
                }
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteF4_flash, "flashStatusClkVddDataVdd");
                var autoConfig4 = ReadRegister(semiconductorModuleContext, BITTestStatusRegister);
                ushort siteAutoConfig4 = autoConfig2.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteAutoConfig4, "autoStatusClkVddDataVdd");

                // POWER CYCLE & PAYLOAD VERIFICATION 

                // Open all the relays which makes a connection between the RTBOOT_L ,RTAD_0 ,RESET_N to the tester instrument to ensure back powering the dead chip
                //(Disable the digital channels connected to the respective pins)
                //tsmContext.ControlRelay(new[] { "RC40", "RC38" }, RelayDriverAction.OpenRelay);
                //tsmContext.ControlRelay(new[] { "RC62" }, RelayDriverAction.OpenRelay);

                DutPowerDownSequence(semiconductorModuleContext);

                Utilities.PreciseWait(0.05);
                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(2.0);

                // Cut power supply and wait 3s for capacitors to discharge
                Utilities.PreciseWait(3.0);

                // Reconnect EEPROM and restore power sequence
                //tsmContext.ControlRelay(new[] { "RC62" }, RelayDriverAction.CloseRelay);
                Utilities.PreciseWait(0.15);

                DutPowerUpSequence(semiconductorModuleContext);

                // Close back the relays which makes the connection between the RTBOOT_L, RTAD_0, RESET_IN to the tester instrument
                //(Enable the digital channels connected to the respective pins)
                //tsmContext.ControlRelay(new[] { "RC40", "RC38" }, RelayDriverAction.CloseRelay);
                Utilities.PreciseWait(0.05);

                // Give the main supply wait time for stabilization
                Utilities.PreciseWait(3.0);

                digitalSessionsBundle.BurstPattern("Reset", true);
                Utilities.PreciseWait(2.0);

                // Verify final post-power-cycle status
                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0004);
                var finalFlashStatus = ReadRegister(semiconductorModuleContext, I2CEEPROMControlStatusRegister); // Expect 0x8000 (No error)

                ushort siteFinalFlashStatus = finalFlashStatus.GetValue(siteNumber);

                semiconductorModuleContext.PublishSingleSiteResult<double>(siteFinalFlashStatus, "flashStatusEEPROM");
                WriteRegister(semiconductorModuleContext, ConfigurationRegister6, 0x0000);


                ushort expectedNoErrorCode = 0x8000;
                if (siteFinalFlashStatus != expectedNoErrorCode)
                {
                    Console.WriteLine($"[ERROR] Post-Power-Cycle Flash Status Error! Read: 0x{finalFlashStatus:X4}, Expected: 0x{expectedNoErrorCode:X4}");
                }

                var finalBitStatus = ReadRegister(semiconductorModuleContext, BITTestStatusRegister); // Expect Auto-init complete & passed

                ushort siteFinalBitStatus = finalFlashStatus.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteFinalBitStatus, "autoConfigStatusEEPROM");
                ushort expectedPassBitStatus = 0xAAAF;
                if (siteFinalBitStatus != expectedPassBitStatus)
                {
                    Console.WriteLine($"[ERROR] Post-Power-Cycle BIT Status Mismatch! Read: 0x{finalBitStatus:X4}, Expected: 0x{expectedPassBitStatus:X4}");
                }

                // Read Memory Payload pushed by EEPROM auto-initialization
                var mem0 = ReadRamSingle(semiconductorModuleContext, 0x0000); // Expect 0xDEAD

                ushort siteMem0 = mem0.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteMem0, "memoryFirstWord");

                var mem1 = ReadRamSingle(semiconductorModuleContext, 0x0001); // Expect 0xBEEF
               
                ushort siteMem1 = mem1.GetValue(siteNumber);
                semiconductorModuleContext.PublishSingleSiteResult<double>(siteMem1, "memorySecondWord");


                if (siteMem0 != 0xDEAD)
                {
                    Console.WriteLine($"[ERROR] Memory Location 0x0000 Payload Mismatch! Read: 0x{mem0:X4}, Expected: 0xDEAD");
                }
                if (siteMem1 != 0xBEEF)
                {
                    Console.WriteLine($"[ERROR] Memory Location 0x0001 Payload Mismatch! Read: 0x{mem1:X4}, Expected: 0xBEEF");
                }

                // Cleanup memory locations for next modules
                WriteRamSingle(semiconductorModuleContext, 0x0000, 0x0000);
                WriteRamSingle(semiconductorModuleContext, 0x0001, 0x0000);

                // TRI-STATE BUFFER INTEGRITY TEST 

                digitalSessionsBundle.ApplyLevelsAndTiming("BU_67833LC", "BU_67833LC");
                digitalSessionsBundle.Do(sessionInfo => {
                    sessionInfo.PinSet.DigitalLevels.Vih = 1.8; // Lower threshold to 1.8V
                });

                WriteRamSingle(semiconductorModuleContext, 0x0000, 0xAAAA); // Inject 0xAAAA into memory

                digitalSessionsBundle.BurstPattern("Reset", true); // Run Tri-state check
                ushort tristatefirstFailingAddress = 0;
                semiconductorModuleContext.PublishSingleSiteResult<double>(tristatefirstFailingAddress, "inputWireCheckTest");

                digitalSessionsBundle.ApplyLevelsAndTiming("BU_67833LC", "BU_67833LC");
                digitalSessionsBundle.Do(session => {
                    session.PinSet.DigitalLevels.Vih = 2.2; // Restore comparators
                });

                DutPowerDownSequence(semiconductorModuleContext);
            }


        }



        
    }
}