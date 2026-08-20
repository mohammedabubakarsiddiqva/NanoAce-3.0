using System;
using System.Linq;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using static BU67833_NEW.DutControl.NanoAceSpi;

namespace BU67833_NEW.TestSteps
{
    public static class SpiVerificationTests
    {
        /// <summary>
        /// Verifies SPI single register and RAM functions by performing write-read-compare operations.
        /// </summary>
        public static void VerifySpiFunctions(ISemiconductorModuleContext siteContext)
        {
            byte regAddr = 0x01;
            ushort regWriteData = 0x000A;

            // 1. Single Register Test
            WriteRegister(siteContext, regAddr, regWriteData);
            SiteData<ushort> regReadData = ReadRegister(siteContext, regAddr);

            // FIX: Convert ushort to double for TSM compatibility using .Select()
            SiteData<double> regReadDouble = regReadData.Select(val => (double)val);
            siteContext.PublishResults(regReadDouble, publishedDataId: "SpiRegSingle_Value");


            // Perform verification logic (SiteData<bool> is a supported type for Pass/Fail evaluation)
            SiteData<bool> regVerifyResult = regReadData.Select(readVal => readVal == regWriteData);
            siteContext.PublishResults(regVerifyResult, publishedDataId: "SpiRegSingle_Verify");


            // 2. RAM Single Test
            WriteRamSingle(siteContext, regAddr, regWriteData);
            SiteData<ushort> memReadData = ReadRamSingle(siteContext, regAddr);

            // FIX: Convert ushort to double for TSM compatibility
            SiteData<double> memReadDouble = memReadData.Select(val => (double)val);
            siteContext.PublishResults(memReadDouble, publishedDataId: "SpiRamSingle_Value");

            // Perform verification logic
            SiteData<bool> memVerifyResult = memReadData.Select(readVal => readVal == regWriteData);
            siteContext.PublishResults(memVerifyResult, publishedDataId: "SpiRamSingle_Verify");
        }
    }
}