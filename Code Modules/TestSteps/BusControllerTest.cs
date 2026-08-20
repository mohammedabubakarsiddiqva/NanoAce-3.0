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
    public static class BusControllerTest
    {
        public static void BCTest(ISemiconductorModuleContext tsmContext, string patternPinsOrPinGroup)
        {
            foreach (ISemiconductorModuleContext semiconductorModuleContext in tsmContext.GetSiteSemiconductorModuleContexts())
            {
                //Apply the relay Configuration to choose the current site 

                int siteNumber = semiconductorModuleContext.SiteNumbers.FirstOrDefault<int>();
                string configName = "DutConfigTxSite" + siteNumber; //(Added by Abhiraj)
                semiconductorModuleContext.ApplyRelayConfiguration(configName); //(Added by Abhiraj)


                //PowerUp the DUT for the current site
                DutPowerUpSequence(semiconductorModuleContext);

                //Creating a session Manager 
                var sessionManager = new TSMSessionManager(semiconductorModuleContext); 

                //Creating a session bundle using the session Manager for digital Pattern Pins
                DigitalSessionsBundle sessionsBundle = sessionManager.Digital(patternPinsOrPinGroup);

                //Create a object for the busCard1 and configuring Channel A of it in the Remote Terminal Mode
                BU67111 busCard1 = new BU67111((ushort)1, "RT");

                //Create a object for the busCard2 and configuring the Channel A of it in the Monitor Terminal Mode
                BU67111 busCard2 = new BU67111((ushort)2, "MT");

                //Setup the Bus Card 2 to setup the MT mode for the Channel A
                busCard2.Setup("A");

                //Start the Monitor Terminal Mode of the BusCard2
                busCard2.Start();

                //Setup the BusCard1 for the Remote Terminal Setup where NanoAce is acting as a bus Controller Mode
                busCard1.BCTestSetup();

                //Reset the NanoAce by writing to the Start Reset Register
                WriteRegister(semiconductorModuleContext, StartResetRegister,0x0001);
                Utilities.PreciseWait(0.1);

                //Configure the NanoAce for the busrst controller mode by bursting the BC_Test_Mem_Test_MEMWRITE_ex1553_CLOCK pattern
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");
                Utilities.PreciseWait(new double?(0.1));
                
                //Configure the Bus Card for the standard Bus Controller Mode
                busCard1.BCTestRemapSA();
                //Configure the NanoAce for the standard Bus Controller Mode
              
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");
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

                //Do a Memory Read operation on the NanoAce for stored content in the Memory of NanoAce in standarad bus controller Mode
                sessionsBundle.BurstPattern("Dummy_Pattern_Read");
                SiteData<HistoryRAMResults> busMemorySiteData = sessionsBundle.FetchHistoryRAMResults();


                double dutMemoryFirstFailingAddresss = 4096.0;
                HistoryRAMResults busMemoryHistoryRamResults = busMemorySiteData.GetValue(siteNumber);
                if (busMemoryHistoryRamResults != null && busMemoryHistoryRamResults.CycleInformation.Count > 0)
                {
                    dutMemoryFirstFailingAddresss = (double)((busMemoryHistoryRamResults.CycleInformation[0].CycleNumber - 10) / 32);
                }

                    
                double busMemoryFirstFailingAddresss = (double)busCard2.CompareBuffer("pathto the standard bus controller mode", "BC", 0x2000);

                //Configure the busCard2 for the Enhanced Bus Controller Mode
                busCard2.EnhancedBCTest();

                //Configure  the NanoAce for the enhanced bus controller Mode by bursting the below pattern enhbctst_Mem_Test_MEMWRITE_ex1553_CLOCK
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Do a Memory Read operation on the NanoAce for stored content in the Memory of NanoAce in enhanced bus controller Mode
                sessionsBundle.BurstPattern("Dummy_Pattern_Read");
                SiteData<HistoryRAMResults> siteData2 = sessionsBundle.FetchHistoryRAMResults();

                double dutEnhMemoryFirstFailingAddresss = 4096.0;
                HistoryRAMResults historyRamResults2 = siteData2.GetValue(siteNumber);
                if (historyRamResults2 != null && historyRamResults2.CycleInformation.Count > 0)
                {
                    dutEnhMemoryFirstFailingAddresss = (double)((historyRamResults2.CycleInformation[0].CycleNumber - 10L) / 32L);
                }
                   //Do a Memory Read Operation on the BusCard for stored content in the Memoy in enhanced bus controller Mode
                double busEnhMemoryFirstFailingAddresss = (double)busCard2.CompareBuffer("path to compare buffer enhanced bus controller mode", "ENHBC", 8192U /*0x2000*/);
                
                busCard2.Reset();
                busCard1.Reset();

                //Power Down the Dut 
                DutPowerDownSequence(semiconductorModuleContext);

                //Publishing the Results
                semiconductorModuleContext.PublishSingleSiteResult<double>(dutMemoryFirstFailingAddresss, "DutMemoryFirstFailingAddress");
                semiconductorModuleContext.PublishSingleSiteResult<double>(busMemoryFirstFailingAddresss, "BusMemoryFirstFailingAddress");
                semiconductorModuleContext.PublishSingleSiteResult<double>(dutEnhMemoryFirstFailingAddresss, "DutEnhMemoryFirstFailingAddress");
                semiconductorModuleContext.PublishSingleSiteResult<double>(busEnhMemoryFirstFailingAddresss, "busEnhMemoryFirstFailingAddress");
            }
        }
    }
}
