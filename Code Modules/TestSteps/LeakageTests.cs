using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;

namespace BU_67833LC.TestSteps
{

    public static class LeakageTests
    {

        public static void PerformLeakage(ISemiconductorModuleContext tsmContext,string[] LeakagePinGroup,double voltageForceValue,double leakageCurrentLimit,double leakageSettlingTime,string publishDataId)
        {
            // 1. Power up the DUT using your custom sequence class
            DutPowerUpSequence(tsmContext);

            // 2. Filter pins to include only those connected to the Digital Pattern instrument
            string[] pins = tsmContext.FilterPinsByInstrumentType(LeakagePinGroup, InstrumentTypeIdConstants.NIDigitalPattern);
    
        // 3. Obtain the digital sessions bundle for the target pins
        var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle digitalSessionsBundle = sessionManager.Digital(pins);
    
        // 4. Pre-condition: Force 0.0 V on all pins to ensure they start grounded
        digitalSessionsBundle.ForceVoltage(0.0, currentLimitRange: leakageCurrentLimit, settlingTime: leakageSettlingTime);
        // 5. Measure leakage sequentially for each pin in the group
        foreach (string requestedPin in pins)
            {
                // Filter the bundle down to a single pin session
                DigitalSessionsBundle pinBundle = digitalSessionsBundle.FilterByPin(requestedPin);

            // Force the leakage force voltage on the active pin
            pinBundle.ForceVoltage(voltageForceValue, currentLimitRange: leakageCurrentLimit, settlingTime: leakageSettlingTime); 

            // Measure the current
            PinSiteData<double> results = pinBundle.MeasureCurrent();

            tsmContext.PublishResults(results, publishDataId); 

            // Return the pin back to 0.0 V before moving to the next pin
            digitalSessionsBundle.ForceVoltage(0.0, currentLimitRange: leakageCurrentLimit, settlingTime: leakageSettlingTime); 
        }

            // 6. Post-test cleanup: Safely disconnect PPMU outputs
            digitalSessionsBundle.DisconnectOutput();
    
        // 7. Power down the DUT
        DutPowerDownSequence(tsmContext);
        }
    }
}
