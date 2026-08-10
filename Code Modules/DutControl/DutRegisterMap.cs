

namespace BU67833_NEW.DutControl
{
    public static class DutRegisterMap
    {
        public const byte InterruptMaskRegister1 = 0x00;
        public const byte ConfigurationRegister1 = 0x01;
        public const byte ConfigurationRegister2 = 0x02;
        public const byte StartResetRegister = 0x03;
        public const byte BCRTCommandStackPointerRegister = 0x03;
        public const byte BCInstructionListPointerRegister = 0x03;
        public const byte BCControlWordRegister = 0x04;
        public const byte RTSubaddressControlWordRegister = 0x04;
        public const byte TimeTagRegister = 0x05;
        public const byte InterruptStatusRegister1 = 0x06;
        public const byte ConfigurationRegister3 = 0x07;
        public const byte ConfigurationRegister4 = 0x08;
        public const byte ConfigurationRegister5 = 0x09;
        public const byte RTMonitorDataStackAddressRegister = 0x0A;
        public const byte BCFrameTimeRemainingRegister = 0x0B;
        public const byte BCTimeRemainingToNextMessageRegister = 0x0C;
        public const byte BCFrameTimeRegister = 0x0D;
        public const byte BCInitialInstructionPointerRegister = 0x0D;
        public const byte RTLastCommandRegister = 0x0D;
        public const byte MTTriggerWordRegister = 0x0D;
        public const byte RTStatusWordRegister = 0x0E;
        public const byte RTBITWordRegister = 0x0F;
        public const byte TestModeRegister0 = 0x10;
        public const byte TestModeRegister1 = 0x11;
        public const byte TestModeRegister2 = 0x12;
        public const byte TestModeRegister3 = 0x13;
        public const byte TestModeRegister4 = 0x14;
        public const byte TestModeRegister5 = 0x15;
        public const byte TestModeRegister6 = 0x16;
        public const byte TestModeRegister7 = 0x17;
        public const byte ConfigurationRegister6 = 0x18;
        public const byte ConfigurationRegister7 = 0x19;
        public const byte Reserved1A = 0x1A;
        public const byte BCConditionCodeRegister = 0x1B;
        public const byte BCGeneralPurposeFlagRegister = 0x1B;
        public const byte BITTestStatusRegister = 0x1C;
        public const byte InterruptMaskRegister2 = 0x1D;
        public const byte InterruptStatusRegister2 = 0x1E;
        public const byte BCGeneralPurposeQueuePointerRegister = 0x1F;
        public const byte RTMTInterruptStatusQueuePointerRegister = 0x1F;
        public const byte AutoConfigurationControlWord = 0x25;
        public const byte AutoConfigurationCheckSumValue = 0x26;
        public const byte AutoConfigurationI2CEEPROMPointer = 0x27;
        public const byte I2CEEPROMWriteDataRegister = 0x2F;
        public const byte I2CEEPROMAddressRegister = 0x30;
        public const byte I2CEEPROMControlStatusRegister = 0x31;
        public const byte I2CEEPROMReadDataRegister = 0x32;
    }
}
