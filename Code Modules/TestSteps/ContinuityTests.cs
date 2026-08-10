using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BU67833_NEW.TestSteps
{
    public static class ContinuityTests
    {

        public static void PerformContinuity(ISemiconductorModuleContext tsmContext, string[] ContinuityPinGroup, double currentForceValue, double voltageLimit, double settlingTime, string publishDataId)
        {


            string[] digitalPins = tsmContext.FilterPinsByInstrumentType(ContinuityPinGroup, InstrumentTypeIdConstants.NIDigitalPattern);
            string[] powerPins = { "VCC_DUT" };

            var sessionManager = new TSMSessionManager(tsmContext);

            //Creating a Session Bundle for the digital Pisn using the session Manager
            var digitalPinsBundle = sessionManager.Digital(digitalPins);
            var dcPowerPinsBundle = sessionManager.DCPower(powerPins);


            //Force 0V to all the Power Pins for Initialization before starting the Continuity Test
            dcPowerPinsBundle.ConfigureSourceDelay(250e-6);
            dcPowerPinsBundle.ForceVoltage(0, currentLimit: 2e-3, waitForSourceCompletion: true);
            Utilities.PreciseWait(1e-3);//Wait 1 ms for settling

            //Forcing 0V to all the digital pins before for initialization before starting the Continuity Test
            digitalPinsBundle.ForceVoltage(0, currentLimitRange: 2e-3);


            Utilities.PreciseWait(1e-3);//Wait 1 ms for settling

            //Force Current : Apply the Requested Current (Positive for Source , Negative for Sink )

            foreach (string pinName in digitalPins)
            {
                //Create a session Bundle for individual pin
                var pinBundle = digitalPinsBundle.FilterByPin(pinName);

                //Force the small current at each pin to measure diode drop voltage
                pinBundle.ForceCurrent(currentLevel: currentForceValue, voltageLimitLow: -voltageLimit, voltageLimitHigh: voltageLimit, settlingTime: settlingTime);

                PinSiteData<double> voltageMeasurements = pinBundle.MeasureVoltage();

                tsmContext.PublishResults(voltageMeasurements, publishDataId);

                pinBundle.ForceVoltage(voltageLevel: 0, currentLimitRange: 0.002);
            }
            digitalPinsBundle.DisconnectOutput();
        }
    }
}
