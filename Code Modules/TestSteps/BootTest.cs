using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Collections.Generic;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;
namespace BU67833_NEW.TestSteps
{
    public static class BootTest
    {
        public static void RtBootTest(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup, string publishingDataId)    
        {

            //Power up the Dut
            DutPowerUpSequence(tsmContext);




            // 2. Initialize TSM Session Manager and retrieve digital sessions
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle pinBundle = sessionManager.Digital(patternPinOrPinGroup);

            // 3. Put all digital pins in the idle state for real-time boot
            var allDigitalBundle = sessionManager.Digital("All_Digital");

            allDigitalBundle.BurstPattern("RtBoot_Idle");

            // 4. Wait 300 ms for boot signals to settle (Uses static utility import for clean syntax)
            PreciseWait(0.3);

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
            pinBundle.ConfigureHistoryRAM(hramSettings);

            // 6. Burst the read pattern to capture the failure cycle 
            pinBundle.BurstPattern("Dummy_Pattern_Read");

            // 7. Fetch captured failure results from History RAM
            SiteData<HistoryRAMResults> siteData = pinBundle.FetchHistoryRAMResults();

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

            // 9. Construct site-aware results and publish to TestStand (Implicitly infers type parameter)
            var results = new SiteData<double>(siteFirstFailingAddresses);
            tsmContext.PublishResults(results, publishingDataId);

            // Power Down the Dut
            DutPowerDownSequence(tsmContext);
        }
    }

    
}

