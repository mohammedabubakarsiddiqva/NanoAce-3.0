using BU67833_NEW.DutControl;
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
using System.Linq;
using static BU67833_NEW.DutControl.DutPowerUpDownSequence;
using static BU67833_NEW.DutControl.DutRegisterMap;
using static BU67833_NEW.DutControl.NanoAceSpi;
using static NationalInstruments.SemiconductorTestLibrary.Common.Utilities;

namespace BU67833_NEW.TestSteps
{
    public static class TransmitterCharacteristicsTest
    {
        public static void TransmitterTest(ISemiconductorModuleContext tsmContext,string patternPinOrPinGroup)
        {
            //Creating site specific semiconductor module contexts from the global semiconductor contexts
            ISemiconductorModuleContext[] semiconductorModuleContexts = tsmContext.GetSiteSemiconductorModuleContexts();

            //Create a session for the External Oscillscope
            InstrumentController scope = new InstrumentController();
            scope.Connect("visa_resource_string_for_oscillscope", true);

            //Create an instrument session for external arbitrary waveform generator
            InstrumentController arb = new InstrumentController();
            arb.Connect("visa_resource_string_for_arb", true);

            foreach (ISemiconductorModuleContext semiconductorModuleContext in semiconductorModuleContexts)
            {
                //Apply the relay configuration for teh current site
                int siteNumber = semiconductorModuleContext.SiteNumbers.FirstOrDefault<int>();
                string configName = "DutConfigTxSite" + siteNumber; //(Added by Abhiraj)
                semiconductorModuleContext.ApplyRelayConfiguration(configName); //(Added by Abhiraj)

                int count = semiconductorModuleContext.SiteNumbers.Count;

                //Power up the dut

                DutPowerUpDownSequence.DutPowerUpSequence(semiconductorModuleContext);

                //Reset the external oscillscope and set the channel 3 display to on state
                scope.Write("*RST");
                scope.Write(":Channel1:DISPLAY 0");
                scope.Write(":Channel2:DISPLAY 0");
                scope.Write(":Channel3:DISPLAY 1");
                scope.Write(":Channel3:DISPLAY 0");

                //Reset the external arbitrary waveform generator 
                arb.Write("*RST");
                arb.Write("OUTP1 OFF");
                PreciseWait(0.01);

                //Create a session Manager
                var sessionManager = new TSMSessionManager(semiconductorModuleContext);

                //Creating a session bundle using the session manager for the pattern pins
                DigitalSessionsBundle sessionsBundle = sessionManager.Digital(patternPinOrPinGroup);

                //Configure the NanoAce for the transmitter test by driving the "Transmitter_updated" pattern
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");

                //Implement the transmitter test for the bus A by calling the below transmitter function
                Transmitter("A", scope, semiconductorModuleContext, tsmContext);

                //Implement the transmitter test for the bus B by calling the below transmitter function
                Transmitter("B", scope, semiconductorModuleContext, tsmContext);

                PreciseWait(new double?(0.01));

                //Run the Idle pattern to restore all the pattern pins to default high Z state
                sessionsBundle.BurstPattern("Dummy_Pattern_Write");
                NationalInstruments.SemiconductorTestLibrary.Common.Utilities.PreciseWait(new double?(0.01));
                scope.Write("*RST");
                scope.Write(":Channel1:DISPLAY 0");
                scope.Write(":Channel2:DISPLAY 0");
                scope.Write(":Channel3:DISPLAY 1");
                scope.Write(":Channel3:DISPLAY 0");


                //Power Down the Dut
                DutPowerDownSequence(semiconductorModuleContext);
            }
            scope.Dispose();
            arb.Dispose();
        }

        public static void Transmitter(string bus, InstrumentController scope,ISemiconductorModuleContext siteContext,ISemiconductorModuleContext globalSiteContext)
        {
            int busIndex = 0;

            if (bus == "A")
            {
                busIndex = 1;
                //Apply the relay configuration for the Channel A
                siteContext.ControlRelay("K8_TX_RX_BUS_SELECT_SCOPE_RELAY", RelayDriverAction.CloseRelay);
            }
            else
            {
                busIndex = 0;
                //Apply the relay configuration for the Channel B
                siteContext.ControlRelay("K8_TX_RX_BUS_SELECT_SCOPE_RELAY", RelayDriverAction.OpenRelay);
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

            scope.Write(":Timebase:Range 5us;Delay 30.7us");
            scope.Write(":Channel3:Range 32V;Offset 0V");
            scope.Write(":Trigger:Source Chan3;Mode Edge;Level 2.8v;slope pos;holdoff 8.5ms");
            scope.Write(":MEAS:SOUR CHAN3");
            scope.Write(":Single");
            
            
            PreciseWait(new double?(0.05));
            scope.Write(":Measure:source Channel3");
            scope.Write(":Acquire:type average;count 8");
            scope.Write(":Run");
            scope.Write(":Measure:Vamplitude?");
            string vAmplitudeStr;
            scope.Read(out vAmplitudeStr);
            double vAmplitude;
            double.TryParse(vAmplitudeStr, out vAmplitude);


            siteContext.PublishSingleSiteResult<double>(vAmplitude, "Amplitude-Channel " + bus);

            //Rise Time Measurement
            scope.Write(":Timebase:Range 1us;Delay 4.5us");
            scope.Write(":Run");
            scope.Write(":Measure:risetime?");
            PreciseWait(new double?(0.05));
            string riseTimeStr;
            scope.Read(out riseTimeStr);
            double riseTime;
            double.TryParse(riseTimeStr, out riseTime);
            if (riseTime < 9.9E+37)
                riseTime *= 1000000000.0;
            siteContext.PublishSingleSiteResult<double>(riseTime, "riseTime-Channel " + bus);

            //Fall time Measurement
            scope.Write(":Timebase:Range 1us;Delay 5us");
            scope.Write(":Run");
            scope.Write(":Measure:falltime?");
            PreciseWait(0.05);

            string fallTimeStr;
            scope.Read(out fallTimeStr);
            double fallTime;
            double.TryParse(fallTimeStr, out fallTime);
            if (fallTime < 9.9E+37)
                fallTime *= 1000000000.0;
            siteContext.PublishSingleSiteResult<double>(fallTime, "fallTime-Channel " + bus);

            //Overshoot Measurement
            double[] overShootResults = new double[4];

            //Calculate the Peak High Voltage (Vmax) of the signal 
            scope.Write(":Timebase:Delay :4.7us");
            scope.Write(":Channel3:Range 10V");
            scope.Write(":Run");
            scope.Write(":digitize channel3");
            scope.Write(":Measure:Vmax?");
            string vMaxStr;
            scope.Read(out vMaxStr);
            double vMax;
            double.TryParse(vMaxStr, out vMax);

            //Calculate the flat high Voltage(Vtop) of the signal
            scope.Write(":Measured:Vtop?");
            string vTopStr;
            scope.Read(out vTopStr);
            double vTop;
            double.TryParse(vTopStr, out vTop);
            overShootResults[0] = vMax - vTop;

            //Calculate the peak low Voltage(Vmin) of the signal
            scope.Write(":Timbase:Delay 5.78us");
            scope.Write(":Channel3:Offset -10V");
            scope.Write(":Run");
            scope.Write(":Measure:Vmin?");
            string vMinStr;
            scope.Read(out vMinStr);
            double vMin;
            double.TryParse(vMinStr, out vMin);

            //Calculate the flat low Voltage(Vbase) of the signal
            scope.Write(":Measure:Vbase?");
            string vBaseStr;
            scope.Read(out vBaseStr);
            double vBase;
            double.TryParse(vBaseStr, out vBase);
            overShootResults[1] = vBase - vMin;

            //Calculate the peak high Voltage(Vmax) of the signal
            scope.Write(":Timebase:range 2us;Delay 660.6us");
            scope.Write(":Channel3:Offset 0V");
            scope.Write(":Run");
            scope.Write(":digitize channel3");
            scope.Write(":Measure:Vmax?");
            scope.Read(out vMaxStr);
            double.TryParse(vMaxStr,out vMax);

            //Calculate the flat high Voltage(Vtop) of the signal
            scope.Write(":Measure:Vtop?");
            scope.Read(out vBaseStr);
            double.TryParse(vBaseStr, out vBase);
            overShootResults[2] = vBase - vMin;

            //Set the bus card to command the NanoAce to transmit 8000 
            busCard1.SetXmitPattern("8000");
            PreciseWait(0.01);

            //Calculate the peak low Voltage(Vmin) of the signal
            scope.Write(":Run");
            scope.Write(":digitize channel3");
            scope.Write(":Measure Vmin?");
            scope.Read(out vMinStr);
            double.TryParse(vMinStr, out vMin);

            //Calcualte teh flat low Voltage(Vbase) of the signal
            scope.Write(":Measure Vbase?");
            scope.Read(out vBaseStr);
            double.TryParse(vBaseStr, out vBase);
            overShootResults[3] = vBase - vMin;

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

            //Calculate the Baseline offset of the signal when dut is transmitting 0000 data
            busCard1.SetXmitPattern("0000");
            scope.Write(":Channel3:Range 10V");
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(0.05);
            string baseLineStr;
            scope.Read(out baseLineStr);
            double baseLine;
            double.TryParse(baseLineStr, out baseLine);

            //Calculate the output offset when the dut is transmitting 0000 pattern
            busCard1.SetXmitPattern("0000");
            PreciseWait(0.01);
            scope.Write(":Timebase:Delay 662us");
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(new double?(0.05));
            string vAverageStr;
            scope.Read(out vAverageStr);
            double vAverage;
            double.TryParse(vAverageStr, out vAverage);
            double outputOffset0000 = vAverage - baseLine;
            if (outputOffset0000 < 9.9E+37)
                outputOffset0000 *= 1000.0;

            //Publish the results for output offset measuremetn when dut is transmitting 0000
            siteContext.PublishSingleSiteResult<double>(outputOffset0000, "offSet(0000)-Channel " + bus);

            //Calculate the output offset of the signal when dut is transmitting FFFF data
            busCard1.SetXmitPattern("FFFF");
            PreciseWait(0.01);
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(0.05);
            scope.Read(out vAverageStr);
            double.TryParse(vAverageStr, out vAverage);
            double outputOffsetFFFF = vAverage - baseLine;
            if (outputOffsetFFFF < 9.9E+37)
                outputOffsetFFFF *= 1000.0;

            //Publish the results for output offset measuremetn when dut is transmitting FFFF
            siteContext.PublishSingleSiteResult<double>(outputOffsetFFFF, "offSet(FFFF)-Channel " + bus);

            //Calculate the output offset of the signal when dut is transmitting 5555 data
            busCard1.SetXmitPattern("5555");
            PreciseWait(0.01);
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(0.05);
            scope.Read(out vAverageStr);
            double.TryParse(vAverageStr, out vAverage);
            double outputOffset5555 = vAverage - baseLine;
            if (outputOffset5555 < 9.9E+37)
                outputOffset5555 *= 1000.0;
            //Publish the results for output offset measuremetn when dut is transmitting 5555
            siteContext.PublishSingleSiteResult<double>(outputOffset5555, "offSet(5555)-Channel " + bus);

            //Calculate the output offset of the signal when dut is transmitting AAAA data
            busCard1.SetXmitPattern("AAAA");
            PreciseWait(0.01);
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(0.05);
            scope.Read(out vAverageStr);
            double.TryParse(vAverageStr, out vAverage);
            double outputOffsetAAAA = vAverage - baseLine;
            if (outputOffsetAAAA < 9.9E+37)
                outputOffsetAAAA *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffsetAAAA, "offSet(AAAA)-Channel " + bus);

            //Calculate the output offset of the signal when dut is transmitting 8000 data
            busCard1.SetXmitPattern("8000");
            PreciseWait(0.01);
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(0.05);
            scope.Read(out vAverageStr);
            double.TryParse(vAverageStr, out vAverage);
            double outputOffset8000 = vAverage - baseLine;
            if (outputOffset8000 < 9.9E+37)
                outputOffset8000 *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffset8000, "offSet(8000)-Channel " + bus);

            //Calculate the output offset of the signal when dut is transmitting 7FFF data
            busCard1.SetXmitPattern("7FFF");
            PreciseWait(0.01);
            scope.Write(":Timebase:Range 1us;Delay 2.5ms");
            scope.Write(":Run");
            scope.Write(":Measure:Vaverage?");
            PreciseWait(new double?(0.05));
            scope.Read(out vAverageStr);
            double.TryParse(vAverageStr, out vAverage);
            double outputOffset7FFF = vAverage - baseLine;
            if (outputOffset7FFF < 9.9E+37)
                outputOffset7FFF *= 1000.0;
            siteContext.PublishSingleSiteResult<double>(outputOffset7FFF, "offSet(7FFF)-Channel " + bus);

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
            scope.Write(":Timebase:Range 5us;Delay 30.7us");
            scope.Write(":Channel3:Range 32V;Offset 0V");
            scope.Write(":Trigger:Source Chan3;Mode Edge;Level 2.8v;slope pos;holdoff 8.5ms");
            scope.Write(":MEAS:SOUR CHAN3");
            scope.Write(":Single");
            PreciseWait(0.05);
            scope.Write(":Measure:source Channel3");
            scope.Write(":Acquire:type average;count 8");
            scope.Write(":Run");
            scope.Write(":Measure:Vamplitude?");
            scope.Read(out string Vamplitude3_3VStr);
            double.TryParse(Vamplitude3_3VStr, out double Vamplitude3_3V);
            siteContext.PublishSingleSiteResult<double>(Vamplitude3_3V, "Amplitude@3.13-Channel " + bus);


            //Calculate the Amplitude of the 1553 signal when the dut voltage is at 3.47Volt
            sessionsBundle.ConfigureSourceDelay(0.00025);
            sessionsBundle.ForceVoltage(supplyMaxThreshold, iLimDut, waitForSourceCompletion: true);
            PreciseWait(0.005);
            scope.Write(":Timebase:Range 5us;Delay 30.7us");
            scope.Write(":Channel3:Range 32V;Offset 0V");
            scope.Write(":Trigger:Source Chan3;Mode Edge;Level 2.8v;slope pos;holdoff 8.5ms");
            scope.Write(":MEAS:SOUR CHAN3");
            scope.Write(":Single");
            PreciseWait(0.05);
            scope.Write(":Measure:source Channel3");
            scope.Write(":Acquire:type average;count 8");
            scope.Write(":Run");
            scope.Write(":Measure:Vamplitude?");
            scope.Read(out string Vamplitude3_47VStr);
            double.TryParse(Vamplitude3_47VStr, out double Vamplitude3_47V);
            siteContext.PublishSingleSiteResult<double>(Vamplitude3_47V, "Amplitude@3.47-Channel " + bus);


            //Transmit Inhibit Test
            tsmSessionManager.Digital("TX_INH").WriteStatic(PinState._1);
            sessionsBundle.ConfigureSourceDelay(0.00025);
            sessionsBundle.ForceVoltage(3.3,iLimDut, waitForSourceCompletion: true);
            PreciseWait(new double?(0.005));
            if (bus == "A")
                busCard1.SetXmitPattern("FFFF");
            else
                busCard1.SetXmitPattern("0000");
            scope.Write(":Timebase:Range 5us;Delay 30.7us");
            scope.Write(":Channel3:Range 32V;Offset 0V");
            scope.Write(":Trigger:Source Chan3;Mode Edge;Level 2.8v;slope pos;holdoff 8.5ms");
            scope.Write(":MEAS:SOUR CHAN3");
            scope.Write(":Single");
            NationalInstruments.SemiconductorTestLibrary.Common.Utilities.PreciseWait(new double?(0.05));
            scope.Write(":Measure:source Channel3");
            scope.Write(":Acquire:type average;count 8");
            scope.Write(":Run");
            scope.Write(":Measure:Vamplitude?");
            string txinhVamplStr;
            scope.Read(out txinhVamplStr);
            double txinhVampl;
            double.TryParse(txinhVamplStr, out txinhVampl);
            if (txinhVampl == 9.9E+37)
            {
                txinhVampl = 0;

            }
                
            busCard1.Dispose();
            siteContext.PublishSingleSiteResult<double>(txinhVampl, "TxInhibit-Channel " + bus);
        }
    }

}

