using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using DDC.Mil1553.Emace;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Linq;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class SupplyCurrent
    {
        public static void SupplyCurrentStatic(ISemiconductorModuleContext tsmContext)
        {
            // Power Up the Dut
            DutPowerUpSequence(tsmContext);

            // 2. Wait 100 ms for the supply rails and device to settle
            PreciseWait(0.1);

            // 3. Initialize TSM Session Manager and retrieve DC Power sessions for the DUT supply
            var sessionManager = new TSMSessionManager(tsmContext);
            DCPowerSessionsBundle sessionsBundle = sessionManager.DCPower("VCC_DUT");

            // 4. Configure high-precision measurement settings (e.g., Aperture Time of 16 ms / ~1 PLC)
            var measureSettings = new DCPowerMeasureSettings
            {
                ApertureTime = 0.016,
                ApertureTimeUnits = DCPowerMeasureApertureTimeUnits.Seconds
            };
            sessionsBundle.ConfigureMeasureSettings(measureSettings);

            // 5. Initiate the SMU hardware trigger and measure current
            sessionsBundle.Initiate();
            PinSiteData<double> results = sessionsBundle.MeasureCurrent();

            // 6. Publish static current results to TestStand (Type parameter is implicitly inferred)
            tsmContext.PublishResults(results, "Icc_Supply_Current_Static");

            // 7. Power down the DUT safely
            DutPowerDownSequence(tsmContext);
        }


        public static void SupplyCurrentDynamic(ISemiconductorModuleContext tsmContext)
        {
            // Loop serially over each active test site to manage shared instrument resources
            foreach (ISemiconductorModuleContext siteContext in tsmContext.GetSiteSemiconductorModuleContexts())
            {
                int siteNumber = siteContext.SiteNumbers.FirstOrDefault();

                // 1. Obtain DC Power sessions mapped specifically to this site's context
                var sessionManager = new TSMSessionManager(siteContext);
                DCPowerSessionsBundle sessionsBundle = sessionManager.DCPower("VCC_DUT");

                // 2. Power up the DUT for the active site
                DutPowerUpSequence(siteContext);

                // Setup the object for the buscard2 and configurig it into BusController Mode
                var bu67111 = new BU67111((ushort)2, "BC");

                // Measure dynamic supply current on Bus A (1) and Bus B (2) sequentially
                for (int busIndex = 1; busIndex <= 2; ++busIndex)
                {
                    if (busIndex == 1)
                    {
                        bu67111.ChangeMsgType("RT-BC");
                        bu67111.Setup("A");
                    }
                    else
                    {
                        bu67111.ChangeMsgType("RT-BC");
                        bu67111.Setup("B");
                        bu67111.SetXmitPattern("0000");
                    }

                    // Begin physical 1553 bus transmission
                    bu67111.Start();

                    // 4. Configure measure settings to integrate current over 1 Power Line Cycle (PLC)
                    var measureSettings = new DCPowerMeasureSettings
                    {
                        ApertureTime = 1.0,
                        ApertureTimeUnits = DCPowerMeasureApertureTimeUnits.PowerLineCycles,
                        MeasureWhen = DCPowerMeasurementWhen.OnDemand
                    };
                    sessionsBundle.ConfigureMeasureSettings(measureSettings);
                    sessionsBundle.Initiate();

                    // Wait 1 ms for SMU conversion/integration settling
                    PreciseWait(0.001);

                    // 5. Measure the current in Amperes and scale to Milliamperes (mA)
                    PinSiteData<double> currentData = sessionsBundle.MeasureCurrent();
                    double currentInMilliAmps = currentData.GetValue(siteNumber, "VCC_DUT") * 1000.0;

                    // 6. Publish the dynamic current measurement for this site and bus combination
                    siteContext.PublishSingleSiteResult(
                        currentInMilliAmps,$"Icc_Supply_Current_Dynamic_bus_{busIndex}","VCC_DUT");
                }

                // 7. Power down the DUT for the active site
                DutPowerDownSequence(siteContext);
            }
        }
    }
}
