using BU67833_NEW.External_OSC_ARB;
using DDC.Mil1553.Emace;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.SemiconductorTestLibrary.Common;
using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.DCPower;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class ReceiverThresholdTest
    {
        public static void ReceiverTest(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup)
        {
            //Create a site specific semiconductor module contexts
            ISemiconductorModuleContext[] semiconductorModuleContexts = tsmContext.GetSiteSemiconductorModuleContexts();

            //Creating a instrumetn session for the external oscillscope
            InstrumentController scope = new InstrumentController();

            //Creating a instrument session for the external arbitrary waveform generator
            InstrumentController arb = new InstrumentController();

            //Establish the connection between scope and tester controller software
            scope.Connect("dummy visa address for the external osc", true);
            //Establish the connection between arb and tester controller software
            arb.Connect("dummy visa address for the external arb", true);


            foreach (ISemiconductorModuleContext semiconductorModuleContext in semiconductorModuleContexts)
            {
                //Apply the relay configuration for the current site

                //Power up the dut for the current site.
                DutPowerUpSequence(semiconductorModuleContext);

                //Reset the external Oscillscope and Arbitrary waveform generator
                arb.Write("*RST");
                scope.Write("*RST");

                //Turn on the display 3 in the external oscillscope
                scope.Write(":Channel1:DISPLAY 0");
                scope.Write(":Channel2:DISPLAY 0");
                scope.Write(":Channel4:DISPLAY 0");
                scope.Write(":Channel3:DISPLAY 1");
                PreciseWait(0.01);

                //Creating a session Manager
               
                var sessionManager = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session bundle using the session manager for the pattern pins
                DigitalSessionsBundle digitalPinBundle = new TSMSessionManager(semiconductorModuleContext).Digital(patternPinOrPinGroup);

                //Burst the Rt_Gen_Mem_Test_MEMWRITE_ex1553_CLOCK_Trial to configure the nanoace into rt mode
                digitalPinBundle.BurstPattern("Dummy_Pattern_Write");


                PreciseWait(0.05);

                //Implement the Receiver Threshold Test for the Bus A
                Receiver("A", semiconductorModuleContext, tsmContext, scope, arb);

                //Implement the Receiver Threshold Test for the Bus B
                Receiver("B", semiconductorModuleContext, tsmContext, scope, arb);

                //Turn off the arbitrary waveform generator and configure teh scope into its default state
                arb.Write(":Output Off");
                scope.Write("Channel3:BWLIMIT 0");
                scope.Write(":Timebase:Mode Main;Range 5us;Ref Center;Delay 30.7us");
                scope.Write(":Channel3:Range 32V;Offset 0V");
                scope.Write(":Trigger:Source Chan3;Mode edge;Level 5v;slope pos;holdoff 700us");
                scope.Write("Acquire:type average;count 8");
                scope.Write(":Measure:source channel3");
                digitalPinBundle.BurstPattern("Dummy_Pattern_Write");

                //Power Down the dut for the current site
                DutPowerDownSequence(semiconductorModuleContext);
            }

            //Close the session for the external oscillscope and arbitrary waveform generator
            scope.Dispose();
            arb.Dispose();
        }

        public static void Receiver(
          string bus,ISemiconductorModuleContext siteContext, ISemiconductorModuleContext globalContext,InstrumentController scope,InstrumentController arb)
        {
            //Reset the NanoAce by writing to the start reset register
            WriteRegister(siteContext, StartResetRegister, 0x0001);
            
            //Configure the NanoAce into remote terminal mode
            WriteRegister(siteContext, ConfigurationRegister1, 0x8FFF);
            if (bus == "A")
            {
                //Apply the relay configruation to choose the busA transformer coupled
            }
            else
            {
                //Apply the relay configuration to choose the busB transfromer coupled
            };
            PreciseWait(0.01);

            //Create a object for the busCard1 with the mode set to Monitor Terminal(MT)
            BU67111 busCard1 = new BU67111(1, "MT");

            //Reset the busCard1
            busCard1.Reset();

            //Clear the instrument error queue and status registers
            arb.Write("*CLS");
            //Turn of the output
            arb.Write("OUTP1 OFF");

            //Load the binary waveform file into external arbitrary waveform generator
            arb.Write("FUNC ARB");
            arb.Write("MMEM:LOAD:DATA 'INT:\\Thresh32.barb");
            PreciseWait(0.5);
            arb.Write("FUNC:ARB 'INT:\\Thresh32.barb'");
            PreciseWait(0.1);

            //Apply a 50 ohm impedance at the output
            arb.Write("OUTP1:LOAD 50");

            //Create a 1.242kHz wave with 3.1 VPP with centered around 0 V
            arb.Write("APPLY:USER 1.242kHZ,3.1 VPP,0V");
            arb.Write("BURS:INT:PER 0.01");
            arb.Write("BURS:NCYC 1000");
            arb.Write("BURS:MODE TRIG");
            arb.Write("TRIG:SOUR BUS");
            arb.Write("BURS:STAT ON");
            arb.Write("OUTP1:TRIG:SLOPE POS");
            arb.Write("OUTP1:TRIG ON");
            arb.Write("OUTP1 ON");
            PreciseWait(0.1);

            //Set up the busCard for the chosen bus
            busCard1.Setup(bus);
            
            //Start the busCard1 into Monitor Terminal
            busCard1.Start();
            PreciseWait(0.1);

            //Configure the External Oscillscope for Vamplittude measurement
            scope.Write("Timebase:Mode Main; Range 5us;Ref Center;Delay 30us");
            scope.Write("Channel3:Range 2V;Offset 0V;BWLIMIT 1");
            scope.Write("Trigger:Source Chan3;Mode Edge;Level 0.2V;slope pos;holdoff 700us");
            scope.Write("Acquire:type normal");
            scope.Write(":Measure:source channel3");
            bool detect = false;
            double minAmpVal = 1.5;
            double maxAmpVal = 4.5;
            double delta = 0.003;
            double ampVal = (minAmpVal + maxAmpVal) / 2.0;
            scope.Write(":Run");
            scope.Write(":digitize channel3");
            scope.Write(":single");
            do
            {
                scope.Write(":Run");
                scope.Write(":digitize channel3");
                scope.Write(":single");

                PreciseWait(0.01);
                ampVal = (minAmpVal + maxAmpVal) / 2.0;
                string str = ampVal.ToString();
                arb.Write("VOLT " + str);
                PreciseWait(new double?(0.1));
                arb.Write("*TRG");
                PreciseWait(new double?(0.01));
                if (busCard1.CheckMTMessages() == 0)
                {
                    maxAmpVal = ampVal;
                    detect = true;
                }
                else
                    minAmpVal = ampVal;
                PreciseWait(new double?(0.01));
            }
            while (maxAmpVal - minAmpVal > delta);

            //Turn off the remote terminal mode of the nano ace by writing to the following register
            WriteRegister(siteContext, StartResetRegister, 0x0FFF);

            PreciseWait(0.01);
            //Measure the final vAmplitude using the scope
            scope.Write(":Measure:Vamplitude?");
            string vAmplitudeStr;
            scope.Read(out vAmplitudeStr);
            double vAmplitude;
            double.TryParse(vAmplitudeStr, out vAmplitude);
            if (detect)
                vAmplitude *= 1000.0;
            else
                vAmplitude = 0.0;
            scope.Write(":Run");
            PreciseWait(0.05);

            //Reset the external busCard and close the instrument session of it
            busCard1.Reset();
            busCard1.Dispose();

            //Publish the results
            siteContext.PublishSingleSiteResult(vAmplitude, "Voltage Threshold Channel " + bus);
        }
    }

}
