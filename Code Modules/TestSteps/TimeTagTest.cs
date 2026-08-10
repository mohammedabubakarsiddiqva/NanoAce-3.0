using BU67833_NEW.DutControl;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
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
    public static class TimeTagTest
    {
        public static void TimeTagRegisterTest(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup, string publishingDataId)
        {
            //Power up the dut
            DutPowerUpSequence(tsmContext);

            //Creating a session Manager
            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating a session bundle using the session Manager for time tag test
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital(patternPinOrPinGroup);

            //Reset the dut to its default state by writing to the start/reset register
            WriteRegister(tsmContext, StartResetRegister, 0x0001);

            //Disable the internal clock of the time tag register and command the dut to use external clock by the following register write
           
            WriteRegister(tsmContext, ConfigurationRegister2,0x380);

            PreciseWait(0.01);

            HistoryRAMSettings settings = new HistoryRAMSettings()
            {
                TriggerSettings = new HistoryRAMTriggerSettings()
                {
                    TriggerType = HistoryRamTriggerType.FirstFailure,
                    PretriggerSamples = 0
                },
                CyclesToAcquire = HistoryRamCycle.Failed,
                MaximumSamplesToAcquirePerSite = 1
            };
            sessionsBundle.ConfigureHistoryRAM(settings);

            //Do the time tag test memory read operation by bursting the below pattern
            sessionsBundle.BurstPattern("Dummy_Pattern_Read");
            SiteData<HistoryRAMResults> siteData = sessionsBundle.FetchHistoryRAMResults();
            Dictionary<int, double> allSitesTimeTagResults = new Dictionary<int, double>();
            foreach (int siteNumber in tsmContext.SiteNumbers)
            {
                double timeTagFirstFailingAddress = 511.0;
                HistoryRAMResults historyRamResults = siteData.GetValue(siteNumber);
                if (historyRamResults != null && historyRamResults.CycleInformation.Count > 0)
                {
                    timeTagFirstFailingAddress = (double)((historyRamResults.CycleInformation[0].CycleNumber - 10L) / 32L);
                }
                   
                allSitesTimeTagResults[siteNumber] = timeTagFirstFailingAddress;
            }
            SiteData<double> results = new SiteData<double>((IDictionary<int, double>)allSitesTimeTagResults);
            tsmContext.PublishResults<double>(results, publishingDataId);

            //Burst the idle pattern to make the pins into default high Z state
            sessionsBundle.BurstPattern("Dummy_Pattern_Write");

            //Power Down the Dut
            DutPowerDownSequence(tsmContext);
        }
    }

}
