using BU67833_NEW.DutControl;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Collections.Generic;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{

    public static class MemoryTests
    {
        public static void MemoryTestIniitalization(ISemiconductorModuleContext tsmContext)
        {
            // 1. Power up the DUT using your custom sequence class
            DutPowerUpSequence(tsmContext);

            // 2. Configure digital output and bidirectional pin groups to High-Z termination mode
            var sessionManager = new TSMSessionManager(tsmContext);
            var digitalPins = new[] { "Digital_Output_Pins", "Digital_Bidirectional" };
            sessionManager.Digital(digitalPins).ConfigureTerminationMode(TerminationMode.HighZ);

            WriteRegister(tsmContext, StartResetRegister, 0X01);
            WriteRegister(tsmContext, ConfigurationRegister2, 0x380);

        }

        public static void MemoryTestBurstPattern(ISemiconductorModuleContext tsmContext, string patternPinOrPinGroup, string writePatternName, string readPatternName, string PublishingDataId)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle patternPinBundle = sessionManager.Digital(patternPinOrPinGroup);

            // Burst the memory test pattern to the dut using the spi protocol
            patternPinBundle.BurstPattern(writePatternName);

            PreciseWait(0.2);

            // 5. Configure History RAM settings to trigger on the first failure of the read sequence
            var hramSettings = new HistoryRAMSettings
            {
                TriggerSettings = new HistoryRAMTriggerSettings
                {
                    TriggerType = HistoryRamTriggerType.FirstFailure,
                    PretriggerSamples = 0
                },
                CyclesToAcquire = HistoryRamCycle.Failed,
                MaximumSamplesToAcquirePerSite = 1
            };
            patternPinBundle.ConfigureHistoryRAM(hramSettings);

            // 6. Burst the read pattern to capture the failure cycle 
            patternPinBundle.BurstPattern("Dummy_Pattern_Read");

            // 7. Fetch captured failure results from History RAM
            SiteData<HistoryRAMResults> siteData = patternPinBundle.FetchHistoryRAMResults();

            // 8. Process captured cycle numbers per active site
            var siteFirstFailingAddresses = new Dictionary<int, double>();
            foreach (int siteNumber in tsmContext.SiteNumbers)
            {
                double firstFailingAddress = 980.0;
                HistoryRAMResults historyRamResults = siteData.GetValue(siteNumber);

                if (historyRamResults?.CycleInformation != null && historyRamResults.CycleInformation.Count > 0)
                {
                    long cycleNumber = historyRamResults.CycleInformation[0].CycleNumber;
                    firstFailingAddress = (double)((cycleNumber - 10) / 32);
                }


                siteFirstFailingAddresses[siteNumber] = firstFailingAddress;
            }

            var results = new SiteData<double>(siteFirstFailingAddresses);
            tsmContext.PublishResults(results, PublishingDataId);
        }

        public static void ParityInitialization(ISemiconductorModuleContext tsmContext, string patternPinOrPinGroup)
        {
            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating a session Bundle using the session Manager for pattern pins
            var sessionBundle = sessionManager.Digital(patternPinOrPinGroup);

            //Reset the dut to factory default state by writing to the Start Reset Register
            WriteRegister(tsmContext, StartResetRegister, 0x0001);

            //Enable the Enhanced features of the Nano Ace by writing to the following register
            NanoAceSpi.WriteRegister(tsmContext, ConfigurationRegister3, 0x8000);

            //Enable the enhanced interrupt features and enable the ram parity by writing to the following register
            NanoAceSpi.WriteRegister(tsmContext, ConfigurationRegister2, 0xC000);

            //Enable the ram parity flag 
            NanoAceSpi.WriteRegister(tsmContext, InterruptMaskRegister1, 0x4000);
        }

        public static void ParityInterruptCheck(ISemiconductorModuleContext tsmContext, string patternPinorPinGroup, string parityreadPattern, string publishingDataId)
        {

            //Creating a session Manager
            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating session bundle for the pattern pins using the session Manager
            var digitalPatternBundle = sessionManager.Digital(patternPinorPinGroup);

            //Enable the access to the hidden test mode registers whose address range from 10 to 17H by the following register
            NanoAceSpi.WriteRegister(tsmContext, ConfigurationRegister4, 0x0005);

            //Enable the wrong parity generation by writing to the test mode register 1 by the following register write
            NanoAceSpi.WriteRegister(tsmContext, TestModeRegister1, 0x4000);

            //Write value 0000H to the ram address 0x0005H which will make the parity generator to generate a wrong parity bit and it will store that parity bit in the 17th bit.
            NanoAceSpi.WriteRamSingle(tsmContext, 0x0005, 0x0000);

            //Disable the wrong parity generation by reseting the test mode register 1 to its default value by writing to the following register
            NanoAceSpi.WriteRegister(tsmContext, TestModeRegister1, 0x0000);

            //Lock the access to the Hidden test mode register by writing to the following register
            NanoAceSpi.WriteRegister(tsmContext, ConfigurationRegister4, 0x0000);

            //Configure the history ram setting to calculate the first failing address
            var hramSettings = new HistoryRAMSettings
            {
                TriggerSettings = new HistoryRAMTriggerSettings
                {
                    TriggerType = HistoryRamTriggerType.FirstFailure,
                    PretriggerSamples = 0
                },
                CyclesToAcquire = HistoryRamCycle.Failed,
                MaximumSamplesToAcquirePerSite = 1
            };
            digitalPatternBundle.ConfigureHistoryRAM(hramSettings);

            // 6. Burst the read pattern to capture the failure cycle 
            digitalPatternBundle.BurstPattern("Dummy_Pattern_Read");

            // 7. Fetch captured failure results from History RAM
            SiteData<HistoryRAMResults> siteData = digitalPatternBundle.FetchHistoryRAMResults();

            // 8. Process captured cycle numbers per active site
            var siteFirstFailingAddresses = new Dictionary<int, double>();
            foreach (int siteNumber in tsmContext.SiteNumbers)
            {
                double firstFailingAddress = 0x0010;
                HistoryRAMResults historyRamResults = siteData.GetValue(siteNumber);

                if (historyRamResults?.CycleInformation != null && historyRamResults.CycleInformation.Count > 0)
                {
                    long cycleNumber = historyRamResults.CycleInformation[0].CycleNumber;
                    firstFailingAddress = (double)((cycleNumber - 10) / 32);
                }


                siteFirstFailingAddresses[siteNumber] = firstFailingAddress;
            }

            // 9. Construct site-aware results and publish to TestStand (Implicitly infers type parameter)
            var results = new SiteData<double>(siteFirstFailingAddresses);
            tsmContext.PublishResults(results, publishingDataId);

            // Power Down the Dut
            DutPowerDownSequence(tsmContext);




        }
        public static void MemoryTestCleanup(ISemiconductorModuleContext tsmContext)
        {
            // 1. Establish session manager and restore digital pins back to active loads
            var sessionManager = new TSMSessionManager(tsmContext);
            var digitalPins = new[] { "Digital_Output_Pins", "Digital_Bidirectional" };


            sessionManager.Digital(digitalPins).ConfigureTerminationMode(TerminationMode.ActiveLoad);

            // 2. Perform the DUT power-down sequence
            DutPowerDownSequence(tsmContext);
        }


    }
}

