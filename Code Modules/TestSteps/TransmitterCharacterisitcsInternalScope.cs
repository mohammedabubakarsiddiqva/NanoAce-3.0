using BU67833_NEW.DutControl;
using BU67833_NEW.External_OSC_ARB;
using DDC.Mil1553.Emace;
using NationalInstruments;
using NationalInstruments.DAQmx;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.ModularInstruments.NIFgen;
using NationalInstruments.ModularInstruments.NIScope;
using NationalInstruments.Restricted;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Fgen;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Scope;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class TransmitterCharacterisitcsInternalScope
    {
        public static void TransmitterTest(ISemiconductorModuleContext tsmContext, string patternPinOrPinGroup)
        {



            //Creating site specific semiconductor module contexts from the global semiconductor contexts
            ISemiconductorModuleContext[] semiconductorModuleContexts = tsmContext.GetSiteSemiconductorModuleContexts();

            foreach (ISemiconductorModuleContext semiconductorModuleContext in semiconductorModuleContexts)
            {
                //Apply the relay configuration for teh current site

                int count = semiconductorModuleContext.SiteNumbers.Count;

                //Power up the dut

                DutPowerUpDownSequence.DutPowerUpSequence(semiconductorModuleContext);


                //Retriving the site specific NI Scope session and mapped instrumetn channel
                NIScope scopeSession;
                string channelList;

                semiconductorModuleContext.GetNIScopeSession("TX_RX_AB_SCOPE", out scopeSession, out channelList);

                //Reset the Intenral scope card before its operation
                scopeSession.Utility.Reset();



                PreciseWait(0.01);

                //Create a session Manager
                var sessionManager = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session bundle using the session manager for the pattern pins
                DigitalSessionsBundle sessionsBundle = sessionManager.Digital(patternPinOrPinGroup);

                //Configure the NanoAce for the transmitter test by driving the "Transmitter_updated" pattern
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Implement the transmitter test for the bus A by calling the below transmitter function
                Transmitter("A", semiconductorModuleContext, tsmContext,scopeSession,channelList);

                //Implement the transmitter test for the bus B by calling the below transmitter function
                Transmitter("B", semiconductorModuleContext, tsmContext, scopeSession, channelList);

                PreciseWait(new double?(0.01));

                //Run the Idle pattern to restore all the pattern pins to default high Z state
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");
                NationalInstruments.SemiconductorTestLibrary.Common.Utilities.PreciseWait(new double?(0.01));
                //Reset the Intenral scope card before its operation
                scopeSession.Utility.Reset();



                //Power Down the Dut
                DutPowerDownSequence(semiconductorModuleContext);
            }

        }

        public static void Transmitter(string bus, ISemiconductorModuleContext siteContext, ISemiconductorModuleContext globalSiteContext, NIScope scopeSession, string channelList)
        {
            int busIndex = 0;

            if (bus == "A")
            {
                busIndex = 1;
                //Apply the relay configuration for the Channel A
            }
            else
            {
                busIndex = 0;
                //Apply the relay configuration for the Channel B
            }

            PreciseWait(new double?(0.05));

            //Apply the relay configuration to choose the transformer coupled bus A connection 
            //Creating a bus card object for the busCard 1 and configure it into a bus controller mode
            var busCard1 = new BU67111((ushort)1, "BC");

            //Reset the busCard1 before its operation
            busCard1.Reset();

            PreciseWait(new double?(0.005));

            //Configure the communication type between NanoAce and BusCard as RT-BC Mode where NanoAce is acting as RT mode while busCard1 asking as BC Mode
            busCard1.ChangeMsgType("RT-BC");

            //Setup the busCard A to act as a bus controller mode and command to transmit message at 25% duty cycle to the chosen channel of the bus card1
            busCard1.Setup(bus);

            //Start the bus Card operation
            busCard1.Start();

            PreciseWait(0.1);

            //Amplitude Measurement at 3.3V device supply voltage

            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            double timeRange = 5e-6; // 5 microseconds
            double sampleRate = 250e6; // 1 GS/s sample rate
            int minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints,referencePosition:50.0,numberOfRecords:1,enforceRealtime:true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList,triggerLevel:2.8, triggerSlope: ScopeTriggerSlope.Positive,triggerCoupling:ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
           // scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
           double[] vAmplitudeArray  = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAmplitude);
            double vAmplitude = vAmplitudeArray.FirstOrDefault();


            siteContext.PublishSingleSiteResult<double>(vAmplitude, "Amplitude-Channel " + bus);

            //Reset the scope 
            scopeSession.Utility.Reset();

            //Rise Time Measurement
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(4.5e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(4.5e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] riseTimeArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.RiseTime);
            double riseTime = riseTimeArray.FirstOrDefault();

            if (riseTime < 9.9E+37)
                riseTime *= 1000000000.0;
            siteContext.PublishSingleSiteResult<double>(riseTime, "riseTime-Channel " + bus);
            //Reset the scope 
            scopeSession.Utility.Reset();


            //Fall time Measurement
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(4.5e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(4.5e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] fallTimeArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.FallTime);
            double fallTime = riseTimeArray.FirstOrDefault();

            if (fallTime < 9.9E+37)
                fallTime *= 1000000000.0;
            siteContext.PublishSingleSiteResult<double>(fallTime, "fallTime-Channel " + bus);
            //Reset the scope 
            scopeSession.Utility.Reset();



            //Overshoot Measurement
            double[] overShootResults = new double[4];

            //Calculate the Peak High Voltage (Vmax) of the signal 
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(4.7e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(4.7e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the peak high voltage(Vmax) of the signal
            double[] voltageMaxArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageMax);
            double voltageMax = voltageMaxArray.FirstOrDefault();

            //Calculate the flat high Voltage(Vtop) of the signal
            double[] voltageTopArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageTop);
            double voltageTop = voltageTopArray.FirstOrDefault();
            overShootResults[0] = voltageMax - voltageTop;

            //Calculate the peak low voltage(Vmin) of the signal
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0;//Actual is -10
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(5.78e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(5.78e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the peak low  voltage(Vmin) of the signal
            double[] voltageMinArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageMin);
            double voltageMin = voltageMaxArray.FirstOrDefault();

            //Retrive  the flat low Voltage(Vbase) of the signal
            double[] voltageBaseArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageBase);
            double voltageBase = voltageTopArray.FirstOrDefault();
          
            overShootResults[1] = voltageBase - voltageMin;

            //Calculate the Peak High Voltage (Vmax) of the signal 
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 2e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(660e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(660e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the peak high voltage(Vmax) of the signal
            voltageMaxArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageMax);
            voltageMax = voltageMaxArray.FirstOrDefault();

            //Calculate the flat high Voltage(Vtop) of the signal
            voltageTopArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageTop);
            voltageTop = voltageTopArray.FirstOrDefault();
            overShootResults[2] = voltageMax - voltageTop;


            //Set the bus card to command the NanoAce to transmit 8000 
            busCard1.SetXmitPattern("8000");
            PreciseWait(0.01);

            //Calculate the peak low voltage(Vmin) of the signal
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0;//Actual is -10
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(5.78e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(5.78e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the peak low  voltage(Vmin) of the signal
            voltageMinArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageMin);
            voltageMin = voltageMaxArray.FirstOrDefault();

            //Retrive  the flat low Voltage(Vbase) of the signal
            voltageBaseArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageBase);
            voltageBase = voltageTopArray.FirstOrDefault();

            overShootResults[3] = voltageBase - voltageMin;

            double overShoot = overShootResults[0];
            for (int index = 0; index < 4; ++index)
            {
                if (overShootResults[index] > overShoot)
                    overShoot = overShootResults[index];
            }
            if (overShoot < 9.9E+37)
                overShoot = overShoot * 1000.0 / 2.0;

            //Publish the Overshoot Results
            siteContext.PublishSingleSiteResult<double>(overShoot, "overShoot-Channel " + bus);
            //Reset the Scope
            scopeSession.Utility.Reset();

            //Calculate the Baseline offset of the signal when dut is transmitting 0000 data
            busCard1.SetXmitPattern("0000");
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] baseLineArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            double baseLine = vAmplitudeArray.FirstOrDefault();
            scopeSession.Utility.Reset();

            //Calculate the output offset when the dut is transmitting 0000 pattern
            busCard1.SetXmitPattern("0000");
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
                        //Retrive the vAmplitude results
            double[] vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            double vAverage = vAmplitudeArray.FirstOrDefault();
            double outputOffset0000 = vAverage - baseLine;
            if (outputOffset0000 < 9.9E+37)
                outputOffset0000 *= 1000.0;

            //Publish the results for output offset measuremetn when dut is transmitting 0000
            siteContext.PublishSingleSiteResult<double>(outputOffset0000, "offSet(0000)-Channel " + bus);

            //Reset the scope
            scopeSession.Utility.Reset();

            //Calculate the output offset of the signal when dut is transmitting FFFF data
            busCard1.SetXmitPattern("FFFF");
            PreciseWait(0.01);
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            //Retrive the vAmplitude results
            vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            vAverage = vAmplitudeArray.FirstOrDefault();
            double outputOffsetFFFF = vAverage - baseLine;
            if (outputOffsetFFFF < 9.9E+37)
                outputOffsetFFFF *= 1000.0;

            //Publish the results for output offset measuremetn when dut is transmitting FFFF
            siteContext.PublishSingleSiteResult<double>(outputOffsetFFFF, "offSet(FFFF)-Channel " + bus);
            //Reset the scope channel
            scopeSession.Utility.Reset();


            //Calculate the output offset of the signal when dut is transmitting 5555 data
            busCard1.SetXmitPattern("5555");
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            //Retrive the vAmplitude results
            vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            vAverage = vAmplitudeArray.FirstOrDefault();
            double outputOffset5555 = vAverage - baseLine;
            if (outputOffset5555 < 9.9E+37)
                outputOffset5555 *= 1000.0;
            //Publish the results for output offset measuremetn when dut is transmitting 5555
            siteContext.PublishSingleSiteResult<double>(outputOffset5555, "offSet(5555)-Channel " + bus);

            //Reset the Scope
            scopeSession.Utility.Reset();


            //Calculate the output offset of the signal when dut is transmitting AAAA data
            busCard1.SetXmitPattern("AAAA");
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            //Retrive the vAmplitude results
            vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            vAverage = vAmplitudeArray.FirstOrDefault();
            double outputOffsetAAAA = vAverage - baseLine;
            if (outputOffsetAAAA < 9.9E+37)
                outputOffsetAAAA *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffsetAAAA, "offSet(AAAA)-Channel " + bus);

            //Reset the Scope
            scopeSession.Utility.Reset();

            //Calculate the output offset of the signal when dut is transmitting 8000 data
            busCard1.SetXmitPattern("8000");
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            //Retrive the vAmplitude results
            vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            vAverage = vAmplitudeArray.FirstOrDefault();

            double outputOffset8000 = vAverage - baseLine;
            if (outputOffset8000 < 9.9E+37)
                outputOffset8000 *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffset8000, "offSet(8000)-Channel " + bus);

            //Reset the Scope
            scopeSession.Utility.Reset();

            //Calculate the output offset of the signal when dut is transmitting 7FFF data
            busCard1.SetXmitPattern("7FFF");
            PreciseWait(0.01);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 10.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 1e-6; // 1 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(2.5e-3));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            //Retrive the vAmplitude results
            vAverageArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAverage);
            vAverage = vAmplitudeArray.FirstOrDefault();
            double outputOffset7FFF = vAverage - baseLine;
            if (outputOffset7FFF < 9.9E+37)
                outputOffset7FFF *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffset7FFF, "offSet(7FFF)-Channel " + bus);
            //Reset the Scope
            scopeSession.Utility.Reset();

            //Calculate the Amplitude for the min and max threshold supply voltage of the dut
            busCard1.SetXmitPattern("0000");
            string[] pins = new string[1] { "VCC_DUT" };
            TSMSessionManager tsmSessionManager = new TSMSessionManager(siteContext);
            DCPowerSessionsBundle sessionsBundle = tsmSessionManager.DCPower(pins);
            double iLimDut = globalSiteContext.GetSpecificationsValue("DC.Ilim_DUT");
            double supplyMaxThreshold = 3.47;
            double supplyMinThreshold = 3.13;
            sessionsBundle.ConfigureSourceDelay(0.00025);

            //Calculate the Amplitude of the  1553 signal when the dut voltage is at 3.13 V
            sessionsBundle.ForceVoltage(supplyMinThreshold, iLimDut, waitForSourceCompletion: true);
            PreciseWait(0.005);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 5e-6; // 5 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] vAmplitudeArray3_3V = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAmplitude);
            double vAmplitude3_3V = vAmplitudeArray3_3V.FirstOrDefault();

            siteContext.PublishSingleSiteResult<double>(vAmplitude3_3V, "Amplitude@3.13-Channel " + bus);
            //Reset the Scope
            scopeSession.Utility.Reset();

            //Calculate the Amplitude of the 1553 signal when the dut voltage is at 3.47Volt
            sessionsBundle.ConfigureSourceDelay(0.00025);
            sessionsBundle.ForceVoltage(supplyMaxThreshold, iLimDut, waitForSourceCompletion: true);
            PreciseWait(0.005);
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 5e-6; // 5 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points
            
            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
           double[] vAmplitudeArray3_47V = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAmplitude);
            double vAmplitude3_47V = vAmplitudeArray.FirstOrDefault();

            siteContext.PublishSingleSiteResult<double>(vAmplitude3_47V, "Amplitude@3.47-Channel " + bus);

            //Reset the Scope
            scopeSession.Utility.Reset();

            //Transmit Inhibit Test
            tsmSessionManager.Digital("TX_INH").WriteStatic(PinState._1);
            sessionsBundle.ConfigureSourceDelay(0.00025);
            sessionsBundle.ForceVoltage(3.3, iLimDut, waitForSourceCompletion: true);
            PreciseWait(new double?(0.005));
            if (bus == "A")
                busCard1.SetXmitPattern("FFFF");
            else
                busCard1.SetXmitPattern("0000");
            // Configure Vertical SubSystem
            scopeSession.Channels[channelList].Range = 32.0;
            scopeSession.Channels[channelList].Offset = 0.0;
            scopeSession.Channels[channelList].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[channelList].Enabled = true;


            //Configure Horizontal SubSystem
            timeRange = 5e-6; // 5 microseconds
            sampleRate = 250e6; // 1 GS/s sample rate
            minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: channelList, triggerLevel: 2.8, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(8.5e-3), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] txInhVampValArray = scopeSession.Channels[channelList].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAmplitude);
            double txInhVampVal = vAmplitudeArray.FirstOrDefault();

            if (txInhVampVal == 9.9E+37)
            {
                txInhVampVal = 0;

            }

            busCard1.Dispose();
            siteContext.PublishSingleSiteResult<double>(txInhVampVal, "TxInhibit-Channel " + bus);
        }
    }
}
