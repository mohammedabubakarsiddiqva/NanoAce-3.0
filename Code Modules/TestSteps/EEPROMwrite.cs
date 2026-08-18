using BU67833_NEW.DutControl;
using DDC.Mil1553.Emace;
using NationalInstruments.ModularInstruments.NIDCPower;

using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System;
using System.Linq;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
namespace BU_67833LC.DutControl
{
    public static partial class EEPROMwrite
    {

        public static void ProgramConfigurationEEPROM(ISemiconductorModuleContext tsmContext)
        {


            ISemiconductorModuleContext[] activeSiteContexts = tsmContext.GetSiteSemiconductorModuleContexts();

            ISemiconductorModuleContext firstSiteContext = activeSiteContexts.FirstOrDefault();
            //Load Record - 1 : Configuring the NanoAce as Remote Terminal Mode(RT)
            //Load Record - 2 : Configuring the NanoAce as Enhanced  Mode
            ushort[] image = { 0x4DDC, 0xEACE, 0x8001, 0x0001, 0x8FFF, 0x8001, 0x0007, 0x8000, 0xC0DE, 0x0B91 };

            // Define bitmasks for register 31H [4, 7]
            const ushort BitOperationComplete = 0x0800; // Bit 11 (Complete) 
            const ushort BitError1_NoAck = 0x0080;      // Bit 7 (Error 1 - No ACK) 
            const ushort BitError2_VerifyFail = 0x0040; // Bit 6 (Error 2 - Verification Mismatch)
            const ushort BitClearErrors = 0x0008;       // Bit 3 (Clears Status flags) 
            const ushort BitStartWrite = 0x0002;        // Bit 1 (Starts Write) 

            // Combined polling and error verification masks [2]
            const ushort PollCompleteMask = (ushort)(BitOperationComplete | BitError1_NoAck | BitError2_VerifyFail);
            const ushort ErrorCheckMask = (ushort)(BitError1_NoAck | BitError2_VerifyFail);


            //Power up the Dut
            DutPowerUpSequence(firstSiteContext);

            //Sof reset of I2C EEPROM
            WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0001); // Soft Reset I2C


            for (ushort addr = 0; addr < image.Length; addr++)
            {

               
                ushort physicalByteAddr = (ushort)(addr * 2);
                bool writeConfirmed = false;

                while (!writeConfirmed)
                {
                    //Load the 16 bit data word
                    WriteRegister(firstSiteContext, I2CEEPROMWriteDataRegister, image[addr]);

                    //Load the physical byte address
                    WriteRegister(firstSiteContext, I2CEEPROMAddressRegister, addr);

                    //Command the NanoAce to initiate the write transaction
                    WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, 0x0002);

                    SiteData<ushort> status;
                    bool allSitesComplete = false;
                    do
                    {
                        status = ReadRegister(firstSiteContext, I2CEEPROMControlStatusRegister);
                        allSitesComplete = status.SiteNumbers.All(site => (status.GetValue(site) & PollCompleteMask) != 0);
                    } while (!allSitesComplete);

                    if (status.SiteNumbers.All(site => (status.GetValue(site) & ErrorCheckMask) == 0))
                    {
                        writeConfirmed = true;
                    }
                    else
                    {
                        WriteRegister(firstSiteContext, I2CEEPROMControlStatusRegister, BitClearErrors);
                    }
                }
            }

            //Power down the dut
            DutPowerDownSequence(firstSiteContext);
           
        }
    }
}
