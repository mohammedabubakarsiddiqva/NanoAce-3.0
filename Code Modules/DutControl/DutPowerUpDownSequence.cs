using System;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.DutControl {

    public static class DutPowerUpDownSequence
    {
        public static void DutPowerUpSequence(ISemiconductorModuleContext tsmContext)
        {
            // 1. Initialize TSM Session Manager
            var sessionManager = new TSMSessionManager(tsmContext);

            // 2. Query digital pins to ensure sessions are active
            sessionManager.Digital("MSTCLR_L");
            sessionManager.Digital("RTBOOT_L");

            // 3. Configure and force power supply (VCC_DUT) using specifications values
            // Note: C# implicitly converts the single string pin into the single-pin overload
            var dcPowerBundle = sessionManager.DCPower("VCC_DUT");
            double iLimDut = tsmContext.GetSpecificationsValue("DC.Ilim_DUT");
            double vccDut = tsmContext.GetSpecificationsValue("DC.Vcc_DUT");

            dcPowerBundle.ConfigureSourceDelay(0.00025);
            dcPowerBundle.ForceVoltage(vccDut, currentLimit: iLimDut, waitForSourceCompletion: true);

            // Wait 5 ms for power to settle
            PreciseWait(0.005);

            // 4. Start generating a 40 MHz clock signal on the CLOCK_IN pin
            var clockInBundle = sessionManager.Digital("CLOCK_IN");
            clockInBundle.Do(sessionInfo =>
                sessionInfo.PinSet.ClockGenerator.GenerateClock(40000000.0, selectDigitalFunction: true)
            );

            // Settle times (400 ms + 1 ms)
            PreciseWait(0.4);
            PreciseWait(0.001);

            // 5. Configure digital pin groups to High-Z (Termination Mode)
            var digitalPins = new[] { "Digital_Output_Pins", "Digital_Bidirectional" };
            var digitalPinsBundle = sessionManager.Digital(digitalPins);
            digitalPinsBundle.ConfigureTerminationMode(TerminationMode.HighZ);

            // 6. Burst startup and configuration patterns
            var allDigitalBundle = sessionManager.Digital("All_Digital");
            allDigitalBundle.BurstPattern("Reset");
            allDigitalBundle.BurstPattern("Idle");

            // 7. Enable Active Load on outputs and bidirectional pins
            digitalPinsBundle.ConfigureTerminationMode(TerminationMode.ActiveLoad);
        }

        public static void DutPowerDownSequence(ISemiconductorModuleContext tsmContext)
        {
            // 1. Initialize TSM Session Manager
            var sessionManager = new TSMSessionManager(tsmContext);

            // 2. Put digital I/O pins into High-Z to prevent current backfeeding during power-down
            var digitalPins = new[] { "Digital_Output_Pins", "Digital_Bidirectional" };
            var digitalPinsBundle = sessionManager.Digital(digitalPins);
            digitalPinsBundle.ConfigureTerminationMode(TerminationMode.HighZ);

            // 3. Burst Idle and PowerDown patterns to cleanly place the DUT into its off state
            var allDigitalBundle = sessionManager.Digital("All_Digital");
            allDigitalBundle.BurstPattern("Idle");
           // allDigitalBundle.BurstPattern("PowerDown");

            // 4. Restore Active Load to output pins
            digitalPinsBundle.ConfigureTerminationMode(TerminationMode.ActiveLoad);

            // 5. Electrically disconnect the digital pattern instrument channels
            allDigitalBundle.DisconnectOutput();

            // 6. Abort generating the master clock signal on CLOCK_IN
            var clockInBundle = sessionManager.Digital("CLOCK_IN");
            clockInBundle.Do(sessionInfo => sessionInfo.PinSet.ClockGenerator.Abort());

            // 7. Ramp down the VCC supply rail to 0.0V at the specified current limit
            var dcPowerBundle = sessionManager.DCPower("VCC_DUT");
            double iLimDut = tsmContext.GetSpecificationsValue("DC.Ilim_DUT");
            double sourceDelayInSeconds = 0.00025;

            dcPowerBundle.ConfigureSourceDelay(sourceDelayInSeconds);
            dcPowerBundle.ForceVoltage(0.0, currentLimit: iLimDut, waitForSourceCompletion: true);
        }
    }
}
