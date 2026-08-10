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
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;
namespace BU_67833LC.DutControl
{
    public static partial class EEPROMwrite
    {
        // Nano-ACE Internal Register Addresses (Hex)
        const byte REG_BIT_STATUS = 0x1C;
        const byte REG_I2C_DATA = 0x2F;
        const byte REG_I2C_ADDRESS = 0x30;
        const byte REG_I2C_CONTROL = 0x31;
        const byte REG_CHECKSUM_VAL = 0x26;
        public static void ProgramConfigurationEEPROM(ISemiconductorModuleContext tsmContext)
        {
            const byte REG_BIT_STATUS = 0x1C;
            const byte REG_CHECKSUM_VAL = 0x26;
            const byte REG_I2C_DATA = 0x2F;
            const byte REG_I2C_ADDRESS = 0x30;
            const byte REG_I2C_CONTROL = 0x31;

            ISemiconductorModuleContext[] activeSiteContexts = tsmContext.GetSiteSemiconductorModuleContexts();

            ISemiconductorModuleContext firstSiteContext = activeSiteContexts.FirstOrDefault();

            ushort[] image = { 0x4DDC, 0xEACE, 0x8101, 0x0001, 0x8FFF, 0x8101, 0x0007, 0x8000, 0xC0DE, 0x0B91 };

            
            WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0001); // Soft Reset I2C
            for (ushort addr = 0; addr < image.Length; addr++)
            {
                bool writeConfirmed = false;
                while (!writeConfirmed)
                {
                    WriteRegister(firstSiteContext, I2CEEPROMWriteDataRegister, image[addr]);
                    WriteRegister(firstSiteContext, I2CEEPROMAddressRegister, addr);
                    WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0002);

                    SiteData<ushort> status;
                    bool allSitesComplete;
                    do
                    {
                        status = ReadRegister(firstSiteContext, I2CEEPROMControlStatusRegister);
                        allSitesComplete = status.SiteNumbers.All(site => (status.GetValue(site) & 0x0800) != 0);
                    } while (!allSitesComplete);

                    if (status.SiteNumbers.All(site => (status.GetValue(site) & 0x0080) == 0))
                    {
                        writeConfirmed = true;
                    }
                    else
                    {
                        WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0008);
                    }
                }
            }
            var sessionManager = new TSMSessionManager(firstSiteContext);
            var resetPin = sessionManager.Digital("MSTCLR_Pin"); 

          
            resetPin.WriteStatic(PinState._0);

            resetPin.WriteStatic(PinState._1);
            bool initComplete = false;
            while (!initComplete)
            {
                SiteData<ushort> bitStatus = ReadRegister(firstSiteContext, BITTestStatusRegister);
                initComplete = bitStatus.SiteNumbers.All(site => (bitStatus.GetValue(site) & 0x0800) != 0);
            }

            
            SiteData<ushort> hwCalculatedSums = ReadRegister(firstSiteContext, AutoConfigurationCheckSumValue);
            
            int firstSite = hwCalculatedSums.SiteNumbers.First();

            image[image.Length - 1] = hwCalculatedSums.GetValue(firstSite);
            
            WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0001);
            for (ushort addr = 0; addr < image.Length; addr++)
            {
                bool writeConfirmed = false;
                while (!writeConfirmed)
                {
                    WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, image[addr]);
                    WriteRegister(firstSiteContext, I2CEEPROMAddressRegister, addr);
                    WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0002);

                    SiteData<ushort> status;
                    bool allSitesComplete;
                    do
                    {
                        status = ReadRegister(firstSiteContext, I2CEEPROMControlStatusRegister);
                        allSitesComplete = status.SiteNumbers.All(site => (status.GetValue(site) & 0x0800) != 0);
                    } while (!allSitesComplete);

                    if (status.SiteNumbers.All(site => (status.GetValue(site) & 0x0080) == 0))
                    {
                        writeConfirmed = true;
                    }
                    else
                    {
                        WriteRegister(tsmContext, REG_I2C_CONTROL, 0x0008);
                    }
                }
            }

            initComplete = false;
            while (!initComplete)
            {
                SiteData<ushort> bitStatus = ReadRegister(firstSiteContext, BITTestStatusRegister);
                initComplete = bitStatus.SiteNumbers.All(site => (bitStatus.GetValue(site) & 0x0800) != 0);
            }

            SiteData<ushort> finalHwSums = ReadRegister(firstSiteContext, AutoConfigurationCheckSumValue);
            SiteData<ushort> bootStatus = ReadRegister(firstSiteContext, REG_I2C_CONTROL);

            //var collector = new ExceptionCollector("EEPROM Final Verification");
            ////var collector = new NaExceptionCollector("EEPROM Final Verification");
            //foreach (var site in firstSiteContext.SiteNumbers)
            //{
            //    ushort actualHwSum = finalHwSums.GetValue(site);
            //    ushort bootCode = (ushort)((bootStatus.GetValue(site) & 0x001E) >> 1);

            //    if (actualHwSum != image[image.Length - 1])
            //    {
            //        collector.Add(new Exception($"Site {site}: Checksum Register (0x{actualHwSum:X4}) mismatch with updated image."), null);
            //    }

            //    if (bootCode == 0x9)
            //    {
            //        collector.Add(new Exception($"Site {site}: Nano-ACE explicitly flagged a checksum mismatch (Boot Code 1001b)."), null);
            //    }
            //}
            //collector.ThrowSTLException();

            //tsmContext.PublishResults(finalHwSums, "Hardware_Calculated_Checksum");
        }
    }
}
