using BU67833_NEW.External_OSC_ARB;
using DDC.Mil1553.Emace;
using NationalInstruments;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.ModularInstruments.NIFgen;
using NationalInstruments.ModularInstruments.NIScope;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class ReceiverThresholdInternalArb
    {
        public static void ReceiverTest(ISemiconductorModuleContext tsmContext, string patternPinOrPinGroup ,string waveformFilePathName)
        {
            //Create a site specific semiconductor module contexts
            ISemiconductorModuleContext[] semiconductorModuleContexts = tsmContext.GetSiteSemiconductorModuleContexts();


            // 1. Load the binary arbitrary waveform once to avoid redundant disk access
            double[] waveformData = ReadBarbWaveformFile(waveformFilePathName);
            int numSamples = waveformData.Length;

            foreach (ISemiconductorModuleContext semiconductorModuleContext in semiconductorModuleContexts)
            {
                //Apply the relay configuration for the current site
                int siteNumber = semiconductorModuleContext.SiteNumbers.FirstOrDefault<int>();
                string configName = "DutConfigTxSite" + siteNumber; //(Added by Abhiraj)
                semiconductorModuleContext.ApplyRelayConfiguration(configName); //(Added by Abhiraj)

                //Power up the dut for the current site.
                DutPowerUpSequence(semiconductorModuleContext);

                //Retriving the site specific NI Scope session and mapped instrumetn channel
                

                semiconductorModuleContext.GetNIScopeSession("TX_RX_AB_SCOPE", out NIScope scopeSession, out string scopeChannelName);
                semiconductorModuleContext.GetNIFGenSession("TX_RX_AB_FGEN_MB", out NIFgen fgen, out string fgenChannelName);

                //Reset the Intenral scope card and fgen  before its operation
                scopeSession.Utility.Reset();
                fgen.Utility.Reset();
                
                PreciseWait(0.01);

                //Creating a session Manager

                var sessionManager = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session bundle using the session manager for the pattern pins
                DigitalSessionsBundle digitalPinBundle = new TSMSessionManager(semiconductorModuleContext).Digital(patternPinOrPinGroup);

                //Burst the Rt_Gen_Mem_Test_MEMWRITE_ex1553_CLOCK_Trial to configure the nanoace into rt mode
                digitalPinBundle.BurstPattern("Dummy_Pattern_Write");


                PreciseWait(0.05);

                //Implement the Receiver Threshold Test for the Bus A
                Receiver("A", semiconductorModuleContext, tsmContext,scopeSession,fgen,scopeChannelName,fgenChannelName,waveformData,numSamples,waveformFilePathName);

                //Implement the Receiver Threshold Test for the Bus B
                Receiver("B", semiconductorModuleContext, tsmContext,scopeSession,fgen, scopeChannelName, fgenChannelName, waveformData, numSamples,waveformFilePathName);

                //Turn off the arbitrary waveform generator and configure teh scope into its default state


                fgen.Utility.Reset();
                scopeSession.Utility.Reset();

                digitalPinBundle.BurstPattern("Dummy_Pattern_Write");

                //Power Down the dut for the current site
                DutPowerDownSequence(semiconductorModuleContext);
            }

        }

        public static void Receiver(
          string bus, ISemiconductorModuleContext siteContext, ISemiconductorModuleContext globalContext,NIScope scopeSession, NIFgen fgen,string scopeChannelName ,string fgenChannelName, double[] waveformData,int numSamples,string waveformFilePathName)
        {
            //Reset the NanoAce by writing to the start reset register
            WriteRegister(siteContext, StartResetRegister, 0x0001);

            //Configure the NanoAce into remote terminal mode
            WriteRegister(siteContext, ConfigurationRegister1, 0x8FFF);
            if (bus == "A")
            {
                //Apply the relay configruation to choose the busA transformer coupled
                siteContext.ControlRelay("K8_TX_RX_BUS_SELECT_SCOPE_RELAY", RelayDriverAction.CloseRelay);

            }
            else
            {
                //Apply the relay configuration to choose the busB transfromer coupled
                siteContext.ControlRelay("K8_TX_RX_BUS_SELECT_SCOPE_RELAY", RelayDriverAction.OpenRelay);
            }
            ;
            PreciseWait(0.01);

            //Create a object for the busCard1 with the mode set to Monitor Terminal(MT)
            BU67111 busCard1 = new BU67111(1, "MT");

            //Reset the busCard1
            busCard1.Reset();

            fgen.Output.SetEnabled(fgenChannelName, true);
            double[] dummywaveform = new double[]{ 0.1,0.2,0.3,0.2,0.1};
          //  int waveformHandle = fgen.Arbitrary.Waveform.CreateChannelWaveformInt16FromFile("", waveformFilePathName,ByteOrder.LittleEndian);
            int waveformHandle = fgen.Arbitrary.Waveform.CreateChannelWaveform("", waveformData);
            fgen.Output.OutputMode = OutputMode.Arbitrary;
            fgen.Output.SetImpedance(fgenChannelName, 50.0);

            double waveFrequency = 1242.0;
            double sampleRate = waveFrequency * numSamples;

            fgen.Arbitrary.SampleRate = sampleRate;
            fgen.Trigger.SetTriggerMode(fgenChannelName, TriggerMode.Burst);

            PreciseWait(0.1);

            //Set up the busCard for the chosen bus
            busCard1.Setup(bus);

            //Start the busCard1 into Monitor Terminal
            busCard1.Start();
            PreciseWait(0.1);



            //Configure the Internal Oscillscope for Vamplittude measurement
            //Amplitude Measurement at 3.3V device supply voltage

            // Configure Vertical SubSystem
            scopeSession.Channels[scopeChannelName].Range = 2.0;
            scopeSession.Channels[scopeChannelName].Offset = 0.0;
            scopeSession.Channels[scopeChannelName].Coupling = ScopeVerticalCoupling.DC;
            scopeSession.Channels[scopeChannelName].Enabled = true;


            //Configure Horizontal SubSystem
            double timeRange = 5e-6; // 5 microseconds
            sampleRate = 250e6;
            int minNumPoints = (int)(timeRange * sampleRate); // 5000 points

            scopeSession.Timing.ConfigureTiming(sampleRate, minNumPoints, referencePosition: 50.0, numberOfRecords: 1, enforceRealtime: true);
            // Configure Edge Triggering

            scopeSession.Trigger.EdgeTrigger.Configure(triggerSource: scopeChannelName, triggerLevel: 0.2, triggerSlope: ScopeTriggerSlope.Positive, triggerCoupling: ScopeTriggerCoupling.DC, triggerHoldoff: PrecisionTimeSpan.FromSeconds(0), triggerDelay: PrecisionTimeSpan.FromSeconds(30.7e-6));
           // scopeSession.Trigger.ConfigureTriggerSoftware(triggerHoldoff: PrecisionTimeSpan.FromSeconds(700e-6), triggerDelay: PrecisionTimeSpan.FromSeconds(30e-6));




            bool detect = false;
            double minAmpVal = 1.5;
            double maxAmpVal = 4.5;
            double delta = 0.003;
            double ampVal = (minAmpVal + maxAmpVal) / 2.0;
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();
            do
            {


                PreciseWait(0.01);
                ampVal = (minAmpVal + maxAmpVal) / 2.0;
         

                // Convert Peak-to-Peak Voltage to Gain (Gain = VPP / 2)
                double gain = ampVal / 2.0;
                double offset = 0.0;

                // DYNAMICALLY update the FGEN output levels using the existing handle
                fgen.Arbitrary.Waveform.Configure(fgenChannelName, waveformHandle, gain, offset);

                // Enable FGEN output and start generating the signal
              
                fgen.InitiateGeneration();
                PreciseWait(new double?(0.1));

                PreciseWait(new double?(0.01));
                if (busCard1.CheckMTMessages() == 0)
                {
                    maxAmpVal = ampVal;
                    detect = true;
                }
                else
                    minAmpVal = ampVal;
                PreciseWait(new double?(0.01));

                fgen.AbortGeneration();
            }
            while (maxAmpVal - minAmpVal > delta);

            //Turn off the remote terminal mode of the nano ace by writing to the following register
            WriteRegister(siteContext, StartResetRegister, 0x0FFF);

            PreciseWait(0.01);
            //Initiate the scope measurement
            scopeSession.Measurement.Initiate();

            //Retrive the vAmplitude results
            double[] vAmplitudeArray = scopeSession.Channels[scopeChannelName].Measurement.FetchScalarMeasurement(timeout: PrecisionTimeSpan.FromSeconds(5), ScopeScalarMeasurementType.VoltageAmplitude);
            double vAmplitude = vAmplitudeArray.FirstOrDefault();

            if (detect)
                vAmplitude *= 1000.0;
            else
                vAmplitude = 0.0;


            PreciseWait(0.05);
            fgen.Output.SetEnabled(fgenChannelName, false);
            fgen.Utility.Reset();
            scopeSession.Utility.Reset();

            //Reset the external busCard and close the instrument session of it
            busCard1.Reset();
            busCard1.Dispose();

            //Publish the results
            siteContext.PublishSingleSiteResult(vAmplitude, "Voltage Threshold Channel " + bus);
        }

        private static double[] ReadBarbWaveformFile(string filePath)
        {
            // Binary reading logic identical to Approach A...
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Waveform file not found: {filePath}");
            }
            byte[] fileBytes = File.ReadAllBytes(filePath);
            // Subtract the 60-byte header before calculating sample count
            int sampleCount = (fileBytes.Length - 60) / sizeof(short);
            double[] data = new double[sampleCount];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                // SKIP THE HEADER: Jump directly to offset 0x3C (60 in decimal)
                stream.Position = 60;

                for (int i = 0; i < sampleCount; i++)
                {
                    short rawSample = reader.ReadInt16();
                    data[i] = (double)rawSample / 32768.0;
                }
            }
            return data;
        }
       
    }

}
