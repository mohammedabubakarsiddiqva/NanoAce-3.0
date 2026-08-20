using static BU67833_NEW.DutControl.NanoAceSpi;
using static BU67833_NEW.DutControl.DutRegisterMap;
using DDC.Mil1553.Emace;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Linq;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{

    public static class RTTest
    {
        public static void RemoteTerminalTest(ISemiconductorModuleContext tsmContext, string patternPinOrPinGroup)
        {
            foreach (ISemiconductorModuleContext semiconductorModuleContext in tsmContext.GetSiteSemiconductorModuleContexts())
            {
                //Apply the site specific relay configuration

                int siteNumber = semiconductorModuleContext.SiteNumbers.FirstOrDefault<int>();
                
                string configName = "DutConfigTxSite" + siteNumber; //(Added by Abhiraj)
                semiconductorModuleContext.ApplyRelayConfiguration(configName); //(Added by Abhiraj)

                //Power Up the Dut for current site
                DutPowerUpSequence(semiconductorModuleContext);

                //Configure the busCard1 into Bus Controller Mode
                BU67111 busCard1 = new BU67111(1, "BC");
                
                //Configure the busCard2 into Remote Terminal Mode
                BU67111 busCard2 = new BU67111(2, "RT");


                PreciseWait(0.05);

                //Creating a session Manager 
                var sessionManger = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session Bundle using the session Manager for digital Pins
                DigitalSessionsBundle sessionsBundle = sessionManger.Digital(patternPinOrPinGroup);

                //Configure the NanoAce into RT Mode and choose the Area A
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Configure the busCard1 into busController Mode and choosing the Area A
                busCard1.RTTestStage1();

                
                PreciseWait(0.05);

                //Configure the busCard2 by setting the RT Address to 0
                busCard2.ChangeRTAddress(0);

                //Setup the Channel A of the bus Card 2 which is in RT Mode
                busCard2.Setup("A");

                //STart the busCard2
                busCard2.Start();

                //Configure the NanoAce into RT Mode and choose the Area B
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Configure the busCard2 by busController Mode and choosing the Area B
                busCard1.RTTestStage2();

                PreciseWait(0.05);
                //Configure the NanoAce to separate the normal messages from the broadcast data 
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Configure the BusCard1 to do broadcast operation
                busCard1.RTTestStage3();

                PreciseWait(0.05);

                //Configure the history Ram Settings for the Memory Read Operation
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

                //Do the Memory read operation on the NanoAce to verify the stored content during the communication between bus card and NanoAce
                sessionsBundle.BurstPattern("Dummy_Pattern_Read");

                SiteData<HistoryRAMResults> siteData = sessionsBundle.FetchHistoryRAMResults();
                double dutMemoryfirstFailingAddress = 4096.0;
                HistoryRAMResults historyRamResults = siteData.GetValue(siteNumber);
                if (historyRamResults != null && historyRamResults.CycleInformation.Count > 0)
                {
                    dutMemoryfirstFailingAddress = ((historyRamResults.CycleInformation[0].CycleNumber - 10L) / 32L);
                }
                    
                double busMemoryFirstFailingAddress = (double)busCard2.CompareBuffer("pathto the compare the stored busController data in RT Test", "RT",0x2000);
                semiconductorModuleContext.PublishSingleSiteResult<double>(dutMemoryfirstFailingAddress, "DutMemoryRTFirstFailingAddress");
                semiconductorModuleContext.PublishSingleSiteResult<double>(busMemoryFirstFailingAddress, "BusMemoryRTFirstFailingAddress");

                //Reset the busCard1 and busCard2
                busCard1.Reset();
                busCard2.Reset();

                //Close the instrument session for busCard 1 and busCard 2
                busCard1.Dispose();
                busCard2.Dispose();
                
                //Power Down the dut
                DutPowerDownSequence(semiconductorModuleContext);
            }
        }
    }

}
