

using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Linq;

namespace BU_67833LC.LoadBoardControl {
    public static class LoadBoardControl
    {
        public static void PowerRelayTemp(ISemiconductorModuleContext tsmContext)
        {
            //Creating a session manager
            var sessionManager = new TSMSessionManager(tsmContext);


            double vccRelay = tsmContext.GetSpecificationsValue("DC.VCC_RELAY");
            double ilimRelay = tsmContext.GetSpecificationsValue("DC.ILIMIT_RELAY");
            double vccTemp = tsmContext.GetSpecificationsValue("DC.VCC_TEMP");
            double ilimTemp = tsmContext.GetSpecificationsValue("DC.ILIMIT_TEMP");
            double tempCircuitSourceDelay = 25e-3;
            double relayCircuitSourceDelay = 25e-3;


            string[] relaySupplyPins = new string[] { "RELAY_SUPPLY_20V", "RELAY_SUPPLY_6V" };

            string[] tempSupplyPins = new string[] { "VCC_TEMP" };

            //Creating a session Bundle using session Manager for the relay supply pins

            var relaySupplyBundle = sessionManager.DCPower(relaySupplyPins);

            var tempSupplyBundle = sessionManager.DCPower(tempSupplyPins);

            //Configuring the source delay for the temperature circuit
            relaySupplyBundle.ConfigureSourceDelay(tempCircuitSourceDelay);

            //Provide supply to the Relay Supply pins
            relaySupplyBundle.ForceVoltage(vccRelay, ilimRelay, waitForSourceCompletion: true);

            //Configuring the source delay for the Relay Supply
            tempSupplyBundle.ConfigureSourceDelay(relayCircuitSourceDelay);

            //Provide supply to the Temp Supply Pins
            tempSupplyBundle.ForceVoltage(vccTemp, ilimTemp, waitForSourceCompletion: true);

        }

        public static void MeasureDaughterBoardTemperature(ISemiconductorModuleContext tsmContext, out double daughterBoardTempC, string tempPin = "TEMP_OUT")
        {
            //Creating a session Manager 
            var sessionManager = new TSMSessionManager(tsmContext); 

            //Creating a session Bundle using session Manager for the temperature out pin
            
            var tempOutSessionBundle = sessionManager.Digital(tempPin);

            //Selecting the PPMU for the analog voltage measurement at tempout pin
            tempOutSessionBundle.SelectPPMU();

            //Measure the Analog Voltage at TEMPOUT pin
            PinSiteData<double> measuredAnalogVoltage = tempOutSessionBundle.MeasureVoltage();

            //Turn of the PPMU
            tempOutSessionBundle.DisconnectOutput();

            //Measure the temperature of the DUT using measured analog voltage
            PinSiteData<double> measuredTemperature = (((measuredAnalogVoltage * 1000.0 * -1.0 + 2230.8) * 0.01732 + 184.470724).SquareRoot() * -1.0 + 13.582) / -0.00866 + 30.0;

            int siteNumber = tsmContext.SiteNumbers.FirstOrDefault();
            daughterBoardTempC = measuredTemperature.GetValue(siteNumber, tempPin);

        }


        public static void ResetRelayConfiguration(ISemiconductorModuleContext tsmContext, string relayConfigurationName)
        {
            tsmContext.ApplyRelayConfiguration(relayConfigurationName);
        }




    }
}

