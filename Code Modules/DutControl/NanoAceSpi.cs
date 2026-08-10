using NationalInstruments.SemiconductorTestLibrary.DataAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction;
using NationalInstruments.SemiconductorTestLibrary.InstrumentAbstraction.Digital;
using NationalInstruments.TestStand.SemiconductorModule.CodeModuleAPI;
using System.Collections.Generic;

namespace BU67833_NEW.DutControl
{
    public static class NanoAceSpi
    {

        public static void WriteRegister(ISemiconductorModuleContext tsmContext, byte addr, ushort data)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");

            // SPI Write payload: [Register Address, Data MSB, Data LSB]
            var waveformData = new uint[]
            {
            addr,
            (uint)(data >> 8),
            (uint)(data & 0xFF) // Cleaned up explicit byte.MaxValue to standard 0xFF
            };

            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Reg_Write", waveformData);
            sessionsBundle.BurstPattern("SPI_Reg_Write");
        }

        public static SiteData<ushort> ReadRegister(ISemiconductorModuleContext tsmContext, byte addr)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");

            var waveformData = new uint[] { addr };

            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Reg_Read", waveformData);
            sessionsBundle.BurstPattern("SPI_Reg_Read");
            SiteData<uint[]> rawCapturedSiteData = sessionsBundle.FetchCaptureWaveform(waveformName: "SPI_Capture_Reg", samplesToRead: 2, timeoutInSeconds: 5.0);

            SiteData<ushort> reconstructedRegisterValues = rawCapturedSiteData.Select(siteSamples =>
            {
                // siteSamples is the uint[] array containing the 2 fetched samples for a SINGLE active site.

                // Extract the First Byte (MSB) captured by the digital instrument
                uint highByte = siteSamples[0];

                // Extract the Second Byte (LSB) captured by the digital instrument
                uint lowByte = siteSamples[1];


                ushort registerWordValue = (ushort)((highByte << 8) | lowByte);

                return registerWordValue;
            });

            return reconstructedRegisterValues;
        }

        public static void WriteRamSingle(ISemiconductorModuleContext tsmContext,ushort addr,ushort data)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");

            // SPI RAM Write payload: [Addr MSB, Addr LSB, Data MSB, Data LSB]
            var waveformData = new uint[]
            {
            (uint)(addr >> 8),
            (uint)(addr & 0xFF),
            (uint)(data >> 8),   
            (uint)(data & 0xFF)
            };

            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Ram_Write", waveformData);
            sessionsBundle.BurstPattern("SPI_Ram_Write");
        }

        public static SiteData<ushort> ReadRamSingle(ISemiconductorModuleContext tsmContext, ushort addr)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");

            // SPI RAM Read Address payload: [Addr MSB, Addr LSB]
            var waveformData = new uint[]
            {
            (uint)(addr >> 8),
            (uint)(addr & 0xFF)
            };

            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Ram_Read", waveformData);
            sessionsBundle.BurstPattern("SPI_Ram_Read");

            SiteData<uint[]> rawCapturedSiteData = sessionsBundle.FetchCaptureWaveform(waveformName: "SPI_Capture_Mem", samplesToRead: 2, timeoutInSeconds: 5.0);

            SiteData<ushort> reconstructedRegisterValues = rawCapturedSiteData.Select(siteSamples =>
            {
                // siteSamples is the uint[] array containing the 2 fetched samples for a SINGLE active site.

                // Extract the First Byte (MSB) captured by the digital instrument
                uint highByte = siteSamples[0];

                // Extract the Second Byte (LSB) captured by the digital instrument
                uint lowByte = siteSamples[1];


                ushort ramWordValue = (ushort)((highByte << 8) | lowByte);

                return ramWordValue;
            });

            return reconstructedRegisterValues;
        }

        public static void WriteRamBurst(ISemiconductorModuleContext tsmContext,ushort addr,byte burstCount,uint[] rawDataWords)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");


            int numWriteClocks = burstCount * 16;
            sessionsBundle.WriteSequencerRegister("reg0", numWriteClocks);

            // 2. Build the SPI payload block
            var waveformData = new List<uint>();

            // Extended Addressing (32K RAM): Addr MSB, Addr LSB 
            waveformData.Add((uint)((addr >> 8) & 0xFF));
            waveformData.Add((uint)(addr & 0xFF));

            // Stream length count byte [1]
            waveformData.Add(burstCount);

            // 3. Serialize 16-bit words into sequential MSB and LSB bytes 
            foreach (uint rawDataWord in rawDataWords)
            {
                waveformData.Add((rawDataWord >> 8) & 0xFF); // Data MSB
                waveformData.Add(rawDataWord & 0xFF);        // Data LSB
            }

            // 4. Load the serialized bytes to instrument memory and execute pattern 
            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Ram_Write", waveformData.ToArray());
            sessionsBundle.BurstPattern("SPI_Ram_Write_Burst");
        }

        public static SiteData<ushort[]> ReadRamBurst(
        ISemiconductorModuleContext tsmContext,
        ushort addr,
        byte burstCount)
        {
            var sessionManager = new TSMSessionManager(tsmContext);
            DigitalSessionsBundle sessionsBundle = sessionManager.Digital("SPI_Pins");

            
            int numReadClocks = burstCount * 16;
            sessionsBundle.WriteSequencerRegister("reg0", numReadClocks);

            // 2. SPI Read Command Payload: [Addr MSB, Addr LSB, Burst Count]
            var commandPayload = new uint[]
            {
            (uint)((addr >> 8) & 0xFF),
            (uint)(addr & 0xFF),
            burstCount
            };

            // 3. Output read command address/length and execute burst pattern
            sessionsBundle.WriteSourceWaveformBroadcast("SPI_Source_Ram_Read", commandPayload);
            sessionsBundle.BurstPattern("SPI_Ram_Read_Burst");

            // 4. Capture MISO response bytes (Each 16-bit word consists of 2 captured bytes) 
            int samplesToRead = burstCount * 2;

            // 5. Fetch captured raw bytes from instrument and project back into 16-bit array per site in parallel 
            return sessionsBundle.FetchCaptureWaveform("SPI_Capture_Mem", samplesToRead)
                                 .Select(siteDataArray =>
                                 {
                                     // Unpacks the flat byte stream into structured 16-bit words
                                     var reconstructedWords = new ushort[burstCount];
                                     for (int i = 0; i < burstCount; i++)
                                     {
                                         uint highByte = siteDataArray[i * 2];
                                         uint lowByte = siteDataArray[i * 2 + 1];
                                         reconstructedWords[i] = (ushort)((highByte << 8) | lowByte);
                                     }
                                     return reconstructedWords;
                                 });
        }


    }
}
