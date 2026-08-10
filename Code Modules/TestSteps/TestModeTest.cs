using BU67833_NEW.DutControl;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System;
using System.Collections.Generic;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class TestModeTest
    {
        public static void TestModeTestInitialization(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup) 
        {
            // 1. Power up the DUT
            DutPowerUpSequence(tsmContext);

            // 2. Initialize TSM Session Manager and configure the Vih input level to 3.0 V
            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating a sesson Bundle using session Manager for the pattern Pins
            var digitalPatternBundle = sessionManager.Digital(patternPinOrPinGroup);

            //Change the input voltage level for all the digital pattern pins for the TestMode Test
            digitalPatternBundle.ConfigureSingleLevel(LevelsAndTiming.LevelType.Vih, 3.0);
            PreciseWait(1.0);
        }
        public static void TestModeBurstPatternTest(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup,string patternName,string publishingDataId,ushort passingValue)        
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle patternPinBundle = sessionManager.Digital(patternPinOrPinGroup);

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
                double firstFailingAddress = passingValue;
                HistoryRAMResults historyRamResults = siteData.GetValue(siteNumber);

                if (historyRamResults?.CycleInformation != null && historyRamResults.CycleInformation.Count > 0)
                {
                    long cycleNumber = historyRamResults.CycleInformation[0].CycleNumber;
                    firstFailingAddress = (double)((cycleNumber - 10) / 32);
                }


                siteFirstFailingAddresses[siteNumber] = firstFailingAddress;
            }

            var results = new SiteData<double>(siteFirstFailingAddresses);
            tsmContext.PublishResults(results, publishingDataId);
        }


        public static void TestModeTestCleanup(ISemiconductorModuleContext tsmContext)
        {
            // 1. Restore standard project levels and timing sheets to prevent state leakage to subsequent steps
            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating a sesson Bundle using session Manager for the pattern Pins
            var digitalPatternBundle = sessionManager.Digital("Pattern_Pins");

            digitalPatternBundle.ApplyLevelsAndTiming("BU_67833LC", "BU_67833LC");

            // 2. Power down the DUT safely
            DutPowerDownSequence(tsmContext);
        }
    }
}
