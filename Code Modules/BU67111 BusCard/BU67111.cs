using System;
using System.IO;
using System.Threading;
using DDC.Mil1553.Emace;
using static AceConstants;
namespace DDC.Mil1553.Emace
{
    public class BU67111 : IDisposable
    {
        private ushort devNum;
        private string bus;
        private string mode;
        private string msgType = "BC-RT";
        private short frameMode = -1;
        private short msgId = 1;
        private short dblkId = 1;
        private short xeqOpcodeId = 1;
        private short calOpcodeId = 2;
        private short mnrFrameId = 1;
        private short mjrFrameId = 2;
        private ushort rtAddr = 1;

        public BU67111(ushort userDevNum, string userMode)
        {
            devNum = userDevNum;
            mode = userMode;
        }

        public void Dispose()
        {
            if (mode == "BC")
            {
                EmaceBU69092.aceBCUninstallHBuf((short)devNum);
                EmaceBU69092.aceBCStop((short)devNum);
            }
            else if (mode == "RT")
            {
                EmaceBU69092.aceRTStop((short)devNum);
            }
            else
            {
                EmaceBU69092.aceMTStop((short)devNum);
            }

            EmaceBU69092.aceFree((short)devNum);
        }

        public void ChangeMode(string newMode)
        {
            this.Reset();
            mode = newMode;
        }

        public void ChangeMsgType(string newType)
        {
            msgType = newType;
        }

        public void SwitchBus()
        {
            short result;

            switch (bus[0])
            {
                case 'A':
                    result = (short)EmaceBU69092.aceBCMsgModify((short)devNum, msgId, 0, ACE_BCCTRL_CHL_B, 0, 0, 0, 0, 0, 0, 0, 0, (BcModifyOption)ACE_BC_MOD_BCCTRL1);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    bus = "B";
                    break;

                case 'B':
                    result = (short)EmaceBU69092.aceBCMsgModify((short)devNum, msgId, 0, (BcCtrlWord)ACE_BCCTRL_CHL_A, 0, 0, 0, 0, 0, 0, 0, 0, (BcModifyOption)ACE_BC_MOD_BCCTRL1);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    bus = "A";
                    break;
            }
        }

        public void DisableTimeTag()
        {
            short result;
            result = (short)EmaceBU69092.aceRegWrite32((short)devNum, 0x404, 0x00000002);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.aceRegWrite32((short)devNum, 0x408, 0x00000000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.aceRegWrite32((short)devNum, 0x40C, 0x0000BEEF);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
        }

        public void SetBCStopOnFrame()
        {
            frameMode = 1;
        }

        public void ClearMTInterrupts()
        {
            EmaceBU69092.aceRegRead32((short)devNum, 0x728);
        }

        public uint CheckMTInterruptStatus()
        {
            return EmaceBU69092.aceRegRead32((short)devNum, 0x728);
        }

        public int CheckMTMessages()
        {
            int totMsgs = 1000;
            int msgsRead = 0;
            int result = 0;
            int totNoRes = 0;
            MSGSTRUCT msg = new MSGSTRUCT();

            int zero_msg_count = 0;

            while (msgsRead < totMsgs)
            {
                result = (int)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                if (result == 1)
                {
                    ++msgsRead;
                    if (((int)msg.wBlkSts & 0x200) != 0)
                    {
                        totNoRes++;
                    }
                }
                else if (result < 0)
                {
                    Console.WriteLine(result);
                    break;
                }

                zero_msg_count = (result == 0) ? zero_msg_count + 1 : zero_msg_count;

                if ((msgsRead > 0) && (zero_msg_count > 10000))
                {
                    break;
                }

                if (((msgsRead == 0) && (zero_msg_count > 150)))
                {
                    totNoRes = 1001;
                    break;
                }
            }

            msgsRead = 0;
            return totNoRes;
        }

        public void SetXmitPattern(string newPattern)
        {
            ushort cmdWrd = 0x0400;
            cmdWrd |= (ushort)(rtAddr << 11);

            switch (newPattern[0])
            {
                case '0':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_0000, 32);
                    cmdWrd = 3104;
                    break;

                case 'F':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_FFFF, 32);
                    cmdWrd = 3136;
                    break;

                case '5':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_5555, 32);
                    cmdWrd = 3168;
                    break;

                case 'A':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_AAAA, 32);
                    cmdWrd = 3200;
                    break;

                case '8':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_8000, 32);
                    cmdWrd = 3232;
                    break;

                case '7':
                    EmaceBU69092.aceBCDataBlkCreate((short)devNum, (short)(dblkId + 1), (BcDataBlkSize)32, bfrs.pattern_7FFF, 32);
                    cmdWrd = 3264;
                    break;
            }

            dblkId += 1;

            EmaceBU69092.aceBCMsgModify((short)devNum, msgId, dblkId, 0, cmdWrd, 0, 0, 0, 0, 0, 0, 0, (BcModifyOption)(ACE_BC_MOD_DBLK1 | ACE_BC_MOD_CMDWRD1_1));
        }

        public void ChangeRTAddress(ushort rtNumber)
        {
            if ((rtNumber < 0) || (rtNumber > 31))
            {
                return;
            }
            else
            {
                rtAddr = rtNumber;
            }
        }

        public void RTTestStage1()
        {
            short result = 0;
            ushort busSelection = 0;
            int subAddress = 0;
            short l_msgId = 1;
            short l_dblkId = 1;
            ushort msgGap = 0;
            ushort[] dblk = new ushort[32];
            short[] opCodes = new short[35];

            result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_BC, 0, 0, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCConfigure((short)devNum, (BcAsyncMode)ACE_BC_ASYNC_HMODE);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCInstallHBuf((short)devNum, 16384);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCOpCodeCreate((short)devNum, 1, (BcOpcode)ACE_OPCODE_DLY, (BcConditionTest)ACE_CNDTST_ALWAYS, 0, 0, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            opCodes[0] = 1;

            result = (short)EmaceBU69092.aceBCFrameCreate((short)devNum, 1, (BcFrameType)ACE_FRAME_MINOR, opCodes, 1, 0, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCOpCodeCreate((short)devNum, 2, (BcOpcode)ACE_OPCODE_CAL, (BcConditionTest)ACE_CNDTST_ALWAYS, 1, 0, 0);
            opCodes[0] = 2;

            result = (short)EmaceBU69092.aceBCFrameCreate((short)devNum, 2, ACE_FRAME_MAJOR, opCodes, 1, 1000, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCStart((short)devNum, 2, -1);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            for (int i = 0; i < 32; i++)
            {
                if (i != 0) { msgGap = (ushort)(99 + (i - 1) * 20); }

                if ((i % 2) == 0) { busSelection = ACE_BCCTRL_CHL_A; }
                else { busSelection = ACE_BCCTRL_CHL_B; }

                if (i < 29) { subAddress = i + 1; }
                else { subAddress = 30; }

                Array.Clear(dblk, 0, dblk.Length);

                if (i < 30)
                {
                    for (int j = 0; j < i + 1; j++)
                    {
                        if (j == 0) { dblk[j] = bfrs.bcrt_dblk[i]; }
                        else if (j == i) { dblk[j] = bfrs.hex_nums[j]; }
                        else { dblk[j] = 0x0000; }
                    }
                }
                else if (i == 30)
                {
                    for (int j = 0; j < 32; j++) { dblk[j] = bfrs.bcrt_dblk_alt[j]; }
                }
                else if (i == 31)
                {
                    for (int j = 0; j < 32; j++) { dblk[j] = bfrs.bcrt_dblk_alt2[j]; }
                }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateBCtoRT((short)devNum, l_msgId, l_dblkId, 1, (ushort)subAddress, (ushort)(i + 1), msgGap, (BcMsgOption)busSelection, dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            for (int i = 0; i < 32; i++)
            {
                if (i != 0) { msgGap = (ushort)(99 + (i - 1) * 20); }
                else { msgGap = 0; }

                if ((i % 2) == 0) { busSelection = ACE_BCCTRL_CHL_A; }
                else { busSelection = ACE_BCCTRL_CHL_B; }

                if (i < 29) { subAddress = i + 1; }
                else { subAddress = 30; }

                Array.Clear(dblk, 0, dblk.Length);

                if (i < 29)
                {
                    for (int j = 0; j < i + 1; j++)
                    {
                        if (i == 0) { dblk[j] = bfrs.rtbc_dblk[i]; }
                        else if (j == i) { dblk[j] = bfrs.rtbc_dblk[i]; }
                        else { dblk[j] = bfrs.hex_nums[j]; }
                    }
                }
                else
                {
                    for (int j = 0; j < 32; j++) { dblk[j] = bfrs.rtbc_dblk_alt[j]; }
                }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateRTtoBC((short)devNum, l_msgId, l_dblkId, 1, (ushort)subAddress, (ushort)(i + 1), msgGap, (BcMsgOption)busSelection, dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }
        }

        public void RTTestStage2()
        {
            short l_msgId = 1;
            short l_dblkId = 1;
            ushort msgGap = 0;
            short result = 0;
            uint msgOptions = 0;
            ushort[] dblk = new ushort[32];
            short[] modeTxRx = { 1, 0, 1, 1, 0, 0 };

            for (int i = 0; i < 5; i++)
            {
                if (i != 0) { msgGap = (ushort)(99 + (i - 1) * 20); }
                else { msgGap = 0; }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateRTtoRT((short)devNum, l_msgId, l_dblkId, 0, 1, (ushort)Math.Pow(2, i), 1, 1, msgGap, (BcMsgOption)ACE_BCCTRL_CHL_A, bfrs.rtrt_dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            for (int i = 0; i < 9; i++)
            {
                if (i != 0)
                {
                    msgGap = (ushort)(99 + (i - 1) * 20);
                    msgOptions = ACE_BCCTRL_CHL_A | ACE_MSGOPT_MODE_SA31;
                }
                else
                {
                    msgGap = 0;
                    msgOptions = ACE_BCCTRL_CHL_A;
                }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, (ushort)i, msgGap, (BcMsgOption)msgOptions, bfrs.rtrt_dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            for (int i = 0; i < 6; i++)
            {
                ushort[] singleDblk = { bfrs.mode_dblk[i] };
                result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, (ushort)modeTxRx[i], (ushort)(i + 16), 0, (BcMsgOption)(ACE_BCCTRL_CHL_A | ACE_MSGOPT_MODE_SA31), singleDblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            Array.Clear(dblk, 0, dblk.Length);
            dblk[0] = 0x5678;
            result = (short)EmaceBU69092.aceBCAsyncMsgCreateBcst((short)devNum, l_msgId, l_dblkId, 17, 1, 0,ACE_BCCTRL_CHL_A, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, 2, 0, (BcMsgOption)ACE_BCCTRL_CHL_A, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            dblk[0] = 0x5555;
            dblk[1] = 0xAAAA;
            result = (short)EmaceBU69092.aceBCAsyncMsgCreateBcst((short)devNum, l_msgId, l_dblkId, 18, 2, 0, ACE_BCCTRL_CHL_A | ACE_BCCTRL_BCST_MSK, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, 2, 0, (BcMsgOption)(ACE_BCCTRL_CHL_A | ACE_BCCTRL_BCST_MSK), dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            for (int i = 0; i < 5; i++)
            {
                result = (short)EmaceBU69092.aceBCAsyncMsgCreateRTtoRT((short)devNum, l_msgId, l_dblkId, 0, 1, (ushort)Math.Pow(2, i), 1, 1, msgGap, ACE_BCCTRL_CHL_B, bfrs.rtrt_dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            for (int i = 0; i < 9; i++)
            {
                if (i != 0)
                {
                    msgGap = (ushort)(99 + (i - 1) * 20);
                    msgOptions = ACE_BCCTRL_CHL_B | ACE_MSGOPT_MODE_SA31;
                }
                else
                {
                    msgGap = 0;
                    msgOptions = ACE_BCCTRL_CHL_B;
                }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, (ushort)i, msgGap, (BcMsgOption)msgOptions, bfrs.rtrt_dblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            for (int i = 0; i < 6; i++)
            {
                ushort[] singleDblk = { bfrs.mode_dblk[i] };
                result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, (ushort)modeTxRx[i], (ushort)(i + 16), 0, (BcMsgOption)(ACE_BCCTRL_CHL_B | ACE_MSGOPT_MODE_SA31), singleDblk);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine("loop " + i + " " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }

            Array.Clear(dblk, 0, dblk.Length);
            dblk[0] = 0x1234;
            result = (short)EmaceBU69092.aceBCAsyncMsgCreateBcst((short)devNum, l_msgId, l_dblkId, 17, 1, 0, ACE_BCCTRL_CHL_B, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, 2, 0, ACE_BCCTRL_CHL_B, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            dblk[0] = 0x1111;
            dblk[1] = 0x2222;
            result = (short)EmaceBU69092.aceBCAsyncMsgCreateBcst((short)devNum, l_msgId, l_dblkId, 18, 2, 0, ACE_BCCTRL_CHL_B | ACE_BCCTRL_BCST_MSK, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, l_msgId, l_dblkId, 1, 1, 2, 0, (BcMsgOption)(ACE_BCCTRL_CHL_B | ACE_BCCTRL_BCST_MSK), dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
        }

        public void RTTestStage3()
        {
            short result;
            ushort busSelection;
            short l_msgId = 1;
            short l_dblkId = 1;
            ushort msgGap = 0;
            ushort[] dblk = new ushort[32];
            short[] wordNums = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 16, 20, 24, 29, 0 };
            short[] subAddrs = { 1, 2, 4, 8, 15, 16, 19, 21, 22, 23, 25, 26, 27, 28, 29, 30 };

            for (int i = 0; i < 16; i++)
            {
                if ((i % 2) == 0) { busSelection = ACE_BCCTRL_CHL_A; }
                else { busSelection = ACE_BCCTRL_CHL_B; }

                Array.Clear(dblk, 0, dblk.Length);

                if (i == 0) { for (int j = 0; j < 16; j++) dblk[j] = bfrs.brcs_dblk[j]; }
                else if (i == 12) { for (int j = 0; j < 20; j++) dblk[j] = bfrs.brcs_dblk_alt[j]; }
                else if (i == 13) { for (int j = 0; j < 24; j++) dblk[j] = bfrs.brcs_dblk_alt2[j]; }
                else if (i == 14) { for (int j = 0; j < 29; j++) dblk[j] = bfrs.brcs_dblk_alt3[j]; }
                else if (i == 15) { for (int j = 0; j < 32; j++) dblk[j] = bfrs.brcs_dblk_alt4[j]; }

                result = (short)EmaceBU69092.aceBCAsyncMsgCreateBcst((short)devNum, l_msgId, l_dblkId, (ushort)subAddrs[i], (ushort)wordNums[i], msgGap, busSelection, dblk);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)l_msgId, 1000);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                Thread.Sleep(1);

                result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, l_dblkId);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }
        }

        public void RTTestMCRST()
        {
            short result;
            ushort[] dblk = new ushort[32];

            result = (short)EmaceBU69092.aceBCAsyncMsgCreateMode((short)devNum, msgId, dblkId, 1, 1, 8, 0, (BcMsgOption)ACE_BCCTRL_CHL_A, dblk);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCSendAsyncMsgHP((short)devNum, (ushort)msgId, 1000);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            Thread.Sleep(1);

            result = (short)EmaceBU69092.aceBCEmptyAsyncList((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceBCDataBlkDelete((short)devNum, dblkId);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
        }

        public void EnhancedBCTest()
        {
            short result = 0;
            ushort rtAdd = 0;
            ushort[] dblk = new ushort[32];
            dblk[0] = 0x0620; dblk[1] = 0xC0DE;

            ushort[] modeData = { 0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017,
                                  0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F };

            result = (short)EmaceBU69092.aceMTStop((short)devNum);

            this.mode = "MRT";

            result = (short)EmaceBU69092.acexMRTConfigure((short)devNum, (RtCmdStkSize)ACE_RT_CMDSTK_2K, (MRtGblDataStkType)ACE_RT_DBLK_GBL_C_128, 50);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            rtAdd = 1;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            for (int i = 1; i < 32; i++)
            {
                Array.Clear(dblk, 0, dblk.Length);
                if (i < 30)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (i == 1) { dblk[j] = bfrs.rtbc_dblk_bc[i - 1]; }
                        else if (j == i - 1) { dblk[j] = bfrs.rtbc_dblk_bc[i - 1]; }
                        else { dblk[j] = bfrs.hex_nums_bc[j]; }
                    }
                }
                else
                {
                    for (int j = 0; j < 32; j++) { dblk[j] = bfrs.rtbc_dblk_alt[j]; }
                }

                result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, (short)(i + 10), (RtDataBlkType)32, dblk, 32);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 50, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_RX, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, (short)(i + 10), (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_TX, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1 > 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (i < 17)
                {
                    result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), modeData[i - 1]);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
            }

            rtAdd = 2;
            ushort[] rt2_sa1 = {0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, 0x2000, 0x4000, 0x8000, 0x0,
                                0x1111, 0x2222, 0x3333,0x4444,0x5555,0x6666,0x7777,0x8888,0x9999,0xAAAA,0xBBBB,0xCCCC,0xDDDD,0xEEEE,0xFFFF};
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 5, (RtDataBlkType)32, rt2_sa1, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 5, 1, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 5, 16, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            rtAdd = 31;
            ushort[] rt31 = { 0x1, 0x640 };
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 6, (RtDataBlkType)32, rt31, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 6, 3, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);

            result = (short)EmaceBU69092.acexMRTStart((short)devNum, -1, 0);
        }

        public int CompareBuffer(string path, string test, uint length)
        {
            MSGSTRUCT msg = new MSGSTRUCT();
            int numLoops = 0;
            int bufCount = 0;
            short result = 0;
            uint gotMsg = 0;
            uint msgLost = 0;
            uint[] buffer = new uint[8192];

            if (length > 8192)
            {
                return -1;
            }

            Array.Clear(buffer, 0, buffer.Length);

            if (test == "RT")
            {
                if (mode != "BC") { return -6; }

                for (int i = 0; i < 32; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceBCGetHBufMsgDecoded((short)devNum, ref msg, ref gotMsg, ref msgLost, ACE_BC_MSGLOC_NEXT_PURGE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -2; }

                        if (gotMsg == 1)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts | 0x00FF);
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = bfrs.stg1_buffer[i];

                            if (i == 0)
                            {
                                buffer[bfrs.stg1_buffer[0]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                                buffer[bfrs.stg1_buffer[0] + 1] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                                buffer[bfrs.stg1_buffer[0] + 2] = msg.wStsWrd1;
                                buffer[bfrs.stg1_buffer[0] + 3] = msg.aDataWrds[0];
                                buffer[bfrs.stg1_buffer[0] + 4] = msg.wStsWrd1;
                            }
                            else
                            {
                                buffer[bfrs.stg1_buffer[i]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                                buffer[bfrs.stg1_buffer[i] + 1] = msg.wCmdWrd1;
                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    buffer[bfrs.stg1_buffer[i] + 2 + j] = msg.aDataWrds[j];
                                    if (j == msg.wWordCount - 1 && i != 14)
                                    {
                                        buffer[bfrs.stg1_buffer[i] + 2 + j + 1] = msg.aDataWrds[j];
                                        buffer[bfrs.stg1_buffer[i] + 3 + msg.wWordCount] = msg.wStsWrd1;
                                    }
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (gotMsg == 0);
                }

                for (int i = 32; i < 64; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceBCGetHBufMsgDecoded((short)devNum, ref msg, ref gotMsg, ref msgLost, ACE_BC_MSGLOC_NEXT_PURGE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -2; }

                        if (gotMsg == 1)
                        {
                            buffer[bufCount++] = (uint)((int)   msg.wBlkSts | 0x00FF);
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = bfrs.stg1_buffer[i];

                            buffer[bfrs.stg1_buffer[i]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                            buffer[bfrs.stg1_buffer[i] + 1] = msg.wCmdWrd1;
                            buffer[bfrs.stg1_buffer[i] + 2] = msg.wBCLoopBack1;
                            buffer[bfrs.stg1_buffer[i] + 3] = msg.wStsWrd1;
                            for (int j = 0; j < msg.wWordCount; j++)
                            {
                                buffer[bfrs.stg1_buffer[i] + 4 + j] = msg.aDataWrds[j];
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (gotMsg == 0);
                }

                int iter = 1;
                bufCount = 0xF00;

                for (int i = 0; i < 24; i++)
                {
                    iter = 1;
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceBCGetHBufMsgDecoded((short)devNum, ref msg, ref gotMsg, ref msgLost, ACE_BC_MSGLOC_NEXT_PURGE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -2; }

                        if (gotMsg == 1)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts | 0x00FF);
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = bfrs.stg2_buffer[i];

                            buffer[bfrs.stg2_buffer[i]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                            if (msg.wCmdWrd2 == 0x0000)
                            {
                                if (((msg.wBCLoopBack1 & 0xFF00) == 0xcd00) || ((msg.wCmdWrd1 & 0xFF00) == 0xfa00))
                                {
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                    for (int j = 0; j < msg.wWordCount; j++)
                                    {
                                        buffer[bfrs.stg2_buffer[i] + iter++] = msg.aDataWrds[j];
                                        if (j == msg.wWordCount - 1)
                                        {
                                            buffer[bfrs.stg2_buffer[i] + iter++] = msg.aDataWrds[j];
                                        }
                                    }
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;
                                }
                                else
                                {
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wBCLoopBack1;
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;

                                    for (int j = 0; j < msg.wWordCount; j++)
                                    {
                                        buffer[bfrs.stg2_buffer[i] + j + iter] = msg.aDataWrds[j];
                                    }
                                }
                            }
                            else
                            {
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd2;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wBCLoopBack1;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;

                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    buffer[bfrs.stg2_buffer[i] + j + iter] = msg.aDataWrds[j];
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (gotMsg == 0);
                }

                for (int i = 24; i < 48; i++)
                {
                    iter = 1;
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceBCGetHBufMsgDecoded((short)devNum, ref msg, ref gotMsg, ref msgLost, ACE_BC_MSGLOC_NEXT_PURGE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -2; }

                        if (gotMsg == 1)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts | 0x00FF);
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = bfrs.stg2_buffer[i];

                            buffer[bfrs.stg2_buffer[i]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                            if (msg.wCmdWrd2 == 0x0000)
                            {
                                if (((msg.wBCLoopBack1 & 0xFF00) == 0xcd00) || ((msg.wCmdWrd1 & 0xFF00) == 0xfa00))
                                {
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                    for (int j = 0; j < msg.wWordCount; j++)
                                    {
                                        buffer[bfrs.stg2_buffer[i] + iter++] = msg.aDataWrds[j];
                                        if (j == msg.wWordCount - 1)
                                        {
                                            buffer[bfrs.stg2_buffer[i] + iter++] = msg.aDataWrds[j];
                                        }
                                    }
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;
                                }
                                else
                                {
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wBCLoopBack1;
                                    buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;
                                    for (int j = 0; j < msg.wWordCount; j++)
                                    {
                                        buffer[bfrs.stg2_buffer[i] + j + iter] = msg.aDataWrds[j];
                                    }
                                }
                            }
                            else
                            {
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd1;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wCmdWrd2;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wBCLoopBack1;
                                buffer[bfrs.stg2_buffer[i] + iter++] = msg.wStsWrd1;

                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    buffer[bfrs.stg2_buffer[i] + j + iter] = msg.aDataWrds[j];
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (gotMsg == 0);
                }

                for (int i = 48; i < 64; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceBCGetHBufMsgDecoded((short)devNum, ref msg, ref gotMsg, ref msgLost, ACE_BC_MSGLOC_NEXT_PURGE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -2; }

                        if (gotMsg == 1)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts | 0x00FF);
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = bfrs.stg2_buffer[i];

                            buffer[bfrs.stg2_buffer[i]] = (uint)((int)msg.wBCCtrlWrd | 0xFF00);
                            buffer[bfrs.stg2_buffer[i] + 1] = msg.wCmdWrd1;
                            for (int j = 0; j < msg.wWordCount; j++)
                            {
                                buffer[bfrs.stg2_buffer[i] + 2 + j] = msg.aDataWrds[j];
                                if (j == msg.wWordCount - 1)
                                {
                                    buffer[bfrs.stg2_buffer[i] + 3 + j] = msg.aDataWrds[j];
                                    buffer[bfrs.stg2_buffer[i] + 3 + msg.wWordCount] = msg.wStsWrd1;
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (gotMsg == 0);
                }

                buffer[0x0101] = 0xffff;
                buffer[0x0104] = 0x0f00;
                buffer[0x0105] = 0xffff;
            }
            else if (test == "BC")
            {
                int iter = 0;
                ushort initOffset = 0x220;
                ushort currOffset = 0x0;

                if (mode != "MT") { return -6; }

                for (int i = 0; i < 32; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg,ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = bfrs.bcrt_buffer_bc[i];
                            buffer[bufCount++] = msg.wCmdWrd1;

                            if (i == 29 || i == 30) { break; }
                            else
                            {
                                currOffset = (ushort)(initOffset + (0x20 * iter++));
                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    if ((msg.wWordCount == 15 && j == 14) || msg.wWordCount == 1)
                                    {
                                        buffer[currOffset + j] = msg.aDataWrds[j];
                                    }
                                    else if (j == msg.wWordCount - 1 && msg.wWordCount != 32)
                                    {
                                        buffer[currOffset + j] = bfrs.hex_nums[j];
                                    }
                                    else
                                    {
                                        buffer[currOffset + j] = msg.aDataWrds[j];
                                    }
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);
                }

                iter = 0;
                for (int i = 0; i < 32; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = bfrs.rtbc_buffer_bc[i];
                            buffer[bufCount++] = msg.wCmdWrd1;

                            for (int j = 0; j < msg.wWordCount; j++)
                            {
                                if (j == msg.wWordCount - 1)
                                {
                                    buffer[0x0620 + (iter * 0x0020) + j] = msg.aDataWrds[j];
                                    if (i == 14) { buffer[0x0620 + (iter * 0x0020) + j + 1] = 0x0de0; }
                                    else { buffer[0x0620 + (iter * 0x0020) + j + 1] = 0xc0de; }
                                }
                                else if (i == 29 || i == 30) { break; }
                                else
                                {
                                    buffer[0x0620 + (iter * 0x0020) + j] = msg.aDataWrds[j];
                                }
                            }

                            if (i == 31)
                            {
                                iter++;
                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    buffer[0x0620 + (iter * 0x0020) + j] = msg.aDataWrds[j];
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);

                    if (!(i == 29 || i == 30)) { iter++; }
                }

                bufCount = 0x0F00;
                for (int i = 0; i < 24; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            ushort cmdWrd = 0x0;

                            if ((msg.wCmdWrd1 & 0xFF00) == 0x0000) { cmdWrd = msg.wCmdWrd2; }
                            else { cmdWrd = msg.wCmdWrd1; }

                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }

                            buffer[bufCount++] = 0xbabe;

                            if (i < 5) { buffer[bufCount++] = msg.aDataWrds[0]; }
                            else if (i > 21) { buffer[bufCount++] = 0x0000; }
                            else { buffer[bufCount++] = (uint)(0x0de0 & (0xFFFF * ((i % 2) == 0 ? 1 : 0))); }
                            buffer[bufCount++] = cmdWrd;
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);
                }

                for (int i = 0; i < 24; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            ushort cmdWrd = 0x0;

                            if ((msg.wCmdWrd1 & 0xFF00) == 0x0000) { cmdWrd = msg.wCmdWrd2; }
                            else { cmdWrd = msg.wCmdWrd1; }

                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }

                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = 0x0000;
                            buffer[bufCount++] = cmdWrd;

                            if (i == 19) { buffer[0x0BE0] = msg.aDataWrds[0]; }
                            else if (i == 20 || i == 22)
                            {
                                for (int j = 0; j < msg.wWordCount; j++)
                                {
                                    buffer[0xA20 + ((i % 20) * 0x0010) + j] = msg.aDataWrds[j];
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);
                }

                buffer[0x104] = 0x0fc0;
                {
                    iter = 0;
                    ushort[] tmp = { 0xc0de, 0x0116, 0x0208, 0x0317, 0x0406, 0x0520, 0x0603, 0x0722, 0x0801, 0x0918 };
                    ushort[] tmp2 = { 0x0c00, 0x0c20, 0x0c40, 0x0c60, 0x0c80, 0x0ca0, 0x0cc0, 0x0ce0,
                                      0x0d00, 0x0d20, 0x0d40, 0x0d60, 0x0d80, 0x0da0, 0x0dc0, 0x0de0,
                                      0x0e00, 0x0e20, 0x0e40, 0x0e60, 0x0e80, 0x0ea0, 0x0ec0, 0x0ee0 };

                    for (int index = 0; index < 10; index++) { buffer[0x600 + iter++] = tmp[index]; }
                    iter = 0;
                    for (int index = 0; index < 24; index++) { buffer[0xc00 + (0x20 * iter++)] = tmp2[index]; }
                }

                for (int i = 0; i < 16; i++)
                {
                    buffer[0x140 + i] = (uint)(0x0200 + (0x0020 * i));
                    buffer[0x150 + i] = (uint)(0x0400 + (0x0020 * i));
                    buffer[0x160 + i] = (uint)(0x0600 + (0x0020 * i));
                    buffer[0x170 + i] = (uint)(0x0800 + (0x0020 * i));

                    buffer[0x1c0 + i] = (uint)(0x0a00 + (0x0020 * i));
                    buffer[0x1d0 + i] = (uint)(0x0a00 + (0x0020 * i));
                    buffer[0x1e0 + i] = (uint)(0x0c00 + (0x0020 * i));
                    buffer[0x1f0 + i] = (uint)(0x0c00 + (0x0020 * i));
                }
                buffer[0x1027] = 0x1000;
                buffer[0x1495] = 0x8000;
            }
            else if (test == "ENHBC")
            {
                int iter = 0;

                if (mode != "MT") { return -6; }

                for (int i = 0; i < 3; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            ushort[] rtbcAddrs = { 0x09c0, 0x0620, 0x0680 };

                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = rtbcAddrs[i];
                            buffer[bufCount++] = msg.wCmdWrd1;

                            for (int j = 0; j < msg.wWordCount; j++)
                            {
                                buffer[rtbcAddrs[i] + j] = msg.aDataWrds[j];
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);
                }

                numLoops = 0;
                do
                {
                    Thread.Sleep(1);
                    result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                    if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                    if (result == 1)
                    {
                        if (((uint)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                        }
                        else
                        {
                            buffer[bufCount++] = (uint)msg.wBlkSts;
                        }
                        buffer[bufCount++] = 0xbabe;
                        buffer[bufCount++] = 0x0600;
                        buffer[bufCount++] = msg.wCmdWrd1;
                    }
                    numLoops++;
                    if (numLoops > 25) { break; }
                } while (result == 0);

                numLoops = 0;
                do
                {
                    Thread.Sleep(1);
                    result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                    if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                    if (result == 1)
                    {
                        if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((uint)msg.wBlkSts & 0xF000) == 0xA000)
                        {
                            buffer[bufCount++] = (uint)((int)msg.wBlkSts & 0xF000);
                        }
                        else
                        {
                            buffer[bufCount++] = (uint)msg.wBlkSts;
                        }
                        buffer[bufCount++] = 0xbabe;
                        buffer[bufCount++] = 0x0300;
                        buffer[bufCount++] = msg.wCmdWrd1;

                        for (int j = 0; j < msg.wWordCount; j++)
                        {
                            buffer[0x0300 + j] = msg.aDataWrds[j];
                        }
                    }
                    numLoops++;
                    if (numLoops > 25) { break; }
                } while (result == 0);

                for (int i = 0; i < 3; i++)
                {
                    numLoops = 0;
                    do
                    {
                        Thread.Sleep(1);
                        result = (short)EmaceBU69092.aceMTGetStkMsgDecoded((short)devNum, ref msg, ACE_MT_MSGLOC_NEXT_PURGE, ACE_MT_STKLOC_ACTIVE);
                        if (result < 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); return -3; }

                        if (result == 1)
                        {
                            ushort[] rtrtAddrs = { 0x0640, 0x09c0, 0x0640 };

                            if (i == 2)
                            {
                                buffer[bufCount++] = 0x4000;
                                buffer[bufCount++] = 0xbabe;
                                buffer[bufCount++] = 0x0260;
                                buffer[bufCount++] = msg.wCmdWrd1;
                            }

                            if (((int)msg.wBlkSts & 0xF000) == 0x8000 || ((int)msg.wBlkSts & 0xF000) == 0xA000)
                            {
                                buffer[bufCount++] = (uint)((uint)msg.wBlkSts & 0xF000);
                            }
                            else
                            {
                                buffer[bufCount++] = (uint)msg.wBlkSts;
                            }
                            buffer[bufCount++] = 0xbabe;
                            buffer[bufCount++] = rtrtAddrs[i];
                            buffer[bufCount++] = msg.wCmdWrd2;

                            for (int j = 0; j < msg.wWordCount; j++)
                            {
                                buffer[rtrtAddrs[i] + j] = msg.aDataWrds[j];
                                if (i == 1)
                                {
                                    buffer[rtrtAddrs[i] + j + 0x0020] = msg.aDataWrds[j];
                                }
                            }
                        }
                        numLoops++;
                        if (numLoops > 25) { break; }
                    } while (result == 0);
                }

                buffer[0x100] = 0x0024;
                buffer[0x104] = 0x0f00;
                {
                    iter = 0;
                    ushort[] tmp = { 0xc0de, 0x0116, 0x0208, 0x0317, 0x0406, 0x0520, 0x0603, 0x0722, 0x0801, 0x0918 };
                    ushort[] tmp2 = { 0x0c00, 0x0c20, 0x0c40, 0x0c60, 0x0c80, 0x0ca0, 0x0cc0, 0x0ce0,
                                      0x0d00, 0x0d20, 0x0d40, 0x0d60, 0x0d80, 0x0da0, 0x0dc0, 0x0de0,
                                      0x0e00, 0x0e20, 0x0e40, 0x0e60, 0x0e80, 0x0ea0, 0x0ec0, 0x0ee0 };
                    for (int i = 0; i < 10; i++) { buffer[0x0600 + iter++] = tmp[i]; }
                    iter = 0;
                    for (int i = 0; i < 24; i++) { buffer[0x0c00 + (0x0020 * iter++)] = tmp2[i]; }
                }

                for (int i = 0; i < 29; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        buffer[0x0620 + (i * 0x0020) + j] = bfrs.hex_nums_bc[j];
                        if ((i == 1) || (i == 3)) { break; }
                        else { buffer[0x0620 + (i * 0x0020) + i] = bfrs.rtbc_dblk_bc[i]; }
                    }
                    if (i == 14) { buffer[0x0620 + (i * 0x0020) + i + 1] = 0x0de0; }
                    else { buffer[0x0620 + (i * 0x0020) + i + 1] = 0xc0de; }
                }

                for (int i = 0; i < 16; i++)
                {
                    buffer[0x140 + i] = (uint)(0x0200 + (0x0020 * i));
                    buffer[0x150 + i] = (uint)(0x0400 + (0x0020 * i));
                    buffer[0x160 + i] = (uint)(0x0600 + (0x0020 * i));
                    buffer[0x170 + i] = (uint)(0x0800 + (0x0020 * i));

                    buffer[0x1c0 + i] = (uint)(0x0a00 + (0x0020 * i));
                    buffer[0x1d0 + i] = (uint)(0x0a00 + (0x0020 * i));
                    buffer[0x1e0 + i] = (uint)(0x0c00 + (0x0020 * i));
                    buffer[0x1f0 + i] = (uint)(0x0c00 + (0x0020 * i));
                }
            }
            else
            {
                return -4;
            }

            int FileLine = 1;

            try
            {
                using (StreamReader CmpFile = new StreamReader(path))
                {
                    int count = 0;
                    int error_cnt = 0;
                    string Line;

                    while ((Line = CmpFile.ReadLine()) != null)
                    {
                        if (Line.Length == 0) continue;

                        for (int index = 0; index < 32 && index + 3 < Line.Length; index += 4)
                        {
                            string word = "";
                            word += Line[index + 2];
                            word += Line[index + 3];
                            word += Line[index];
                            word += Line[index + 1];

                            uint val = 0;
                            try
                            {
                                val = Convert.ToUInt32(word, 16);
                            }
                            catch { }

                            if (buffer[count] != val)
                            {
                                error_cnt++;
                                Console.WriteLine($"Error #: {error_cnt}   File Line: {FileLine} : 0x{val:X}   Buffer[{count}]: 0x{buffer[count]:X} ***");
                                return FileLine;
                            }
                            count++;
                        }
                        FileLine++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error opening file: {path}");
                Console.Error.WriteLine($"Error details: {ex.Message}");
                return 0;
            }

            int res = 0x1000;
            return res;
        }

        public int compareBinary32_explicit(string path, uint[] buffer, uint bufferWords16, bool useBigEndian, long skipBytes)
        {
            try
            {
                using (BinaryReader f = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    if (skipBytes > 0)
                    {
                        f.BaseStream.Seek(skipBytes, SeekOrigin.Begin);
                    }

                    for (uint i = 0; i < bufferWords16; ++i)
                    {
                        byte b0 = 0, b1 = 0;

                        try
                        {
                            b0 = f.ReadByte();
                            b1 = f.ReadByte();
                        }
                        catch (EndOfStreamException)
                        {
                            return (int)(i + 1);
                        }

                        ushort fileVal = (ushort)((b0 << 8) | b1);
                        ushort bufVal = (ushort)(buffer[i] & 0xFFFF);

                        if (bufVal != fileVal)
                        {
                            Console.Error.WriteLine($"Mismatch at word {i + 1} bytes=[{b0:X} {b1:X}] file=0x{fileVal:X} buffer=0x{bufVal:X} (raw32=0x{buffer[i]:X})");
                            return (int)(i + 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Cannot open file: {path} - {ex.Message}");
                return -1;
            }

            return 0x1000;
        }

        public void Setup(string bus)
        {
            short i = 0;
            short result;
            ushort busSelection;

            this.bus = bus;

            if (bus == "A")
            {
                busSelection = ACE_BCCTRL_CHL_A;
            }
            else
            {
                busSelection = ACE_BCCTRL_CHL_B;
            }

            if (mode == "BC")
            {
                short[] opcodes = new short[10];

                result = (short)EmaceBU69092.aceFree((short)devNum);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_BC, 0, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (msgType == "BC-RT")
                {
                    result = (short)EmaceBU69092.aceBCDataBlkCreate((short)devNum, dblkId, (BcDataBlkSize)32, bfrs.pattern_5555, 32);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    result = (short)EmaceBU69092.aceBCMsgCreateBCtoRT((short)devNum, msgId, dblkId, rtAddr, 1, 32, 2600, (BcMsgOption)busSelection);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
                else if (msgType == "RT-BC")
                {
                    result = (short)EmaceBU69092.aceBCDataBlkCreate((short)devNum, dblkId, (BcDataBlkSize)32, bfrs.hex_nums_bc, 32);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    result = (short)EmaceBU69092.aceBCMsgCreateRTtoBC((short)devNum, msgId, dblkId, rtAddr, 1, 32, 2600, (BcMsgOption)busSelection);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
                else if (msgType == "RT-RT")
                {
                    ushort rtAddr2;

                    if (rtAddr != 32) { rtAddr2 = (ushort)(rtAddr + 1); }
                    else { rtAddr2 = (ushort)(rtAddr - 1); }

                    result = (short)EmaceBU69092.aceBCDataBlkCreate((short)devNum, dblkId, (BcDataBlkSize)32, bfrs.pattern_rt1, 32);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    result = (short)EmaceBU69092.aceBCDataBlkCreate((short)devNum, 100, (BcDataBlkSize)32, bfrs.pattern_rt2, 32);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    result = (short)EmaceBU69092.aceBCMsgCreateRTtoRT((short)devNum, msgId, dblkId, rtAddr2, 2, 32, rtAddr, 1, 2600, (BcMsgOption)busSelection);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
                else
                {
                    result = (short)EmaceBU69092.aceBCDataBlkCreate((short)devNum, dblkId, (BcDataBlkSize)32, bfrs.pattern_0000, 32);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    result = (short)EmaceBU69092.aceBCMsgCreateBCtoRT((short)devNum, msgId, dblkId, rtAddr, 1, 32, 2600, (BcMsgOption)busSelection);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }

                result = (short)EmaceBU69092.aceBCOpCodeCreate((short)devNum, xeqOpcodeId, (BcOpcode)ACE_OPCODE_XEQ, (BcConditionTest)ACE_CNDTST_ALWAYS, (uint)msgId, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                opcodes[0] = xeqOpcodeId;
                opcodes[1] = calOpcodeId;

                result = (short)EmaceBU69092.aceBCFrameCreate((short)devNum, mnrFrameId, (BcFrameType)ACE_FRAME_MINOR, opcodes, 1, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCOpCodeCreate((short)devNum, calOpcodeId,(BcOpcode)ACE_OPCODE_CAL, (BcConditionTest)ACE_CNDTST_ALWAYS, (uint)mnrFrameId, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceBCFrameCreate((short)devNum, mjrFrameId, ACE_FRAME_MAJOR, opcodes, 1, 5200, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }
            else if (mode == "RT")
            {
                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_RT, 0, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, dblkId, (RtDataBlkType)ACE_RT_DBLK_SINGLE, bfrs.pattern_biurt, 32);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                result = (short)EmaceBU69092.aceRTSetAddress((short)devNum, rtAddr);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                for (i = 0; i < 32; i++)
                {
                    result = (short)EmaceBU69092.aceRTDataBlkMapToSA((short)devNum, (ushort)dblkId, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_ALL, 0, 1>0);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
            }
            else
            {
                result = (short)EmaceBU69092.aceFree((short)devNum);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_MT, 0, 0, 0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            }
        }

        public void BCTestSetup()
        {
            short result = 0;
            ushort rtAdd = 0;
            ushort[] dblk = new ushort[32];
            dblk[0] = 0x0620;

            ushort[] modeData = { 0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017,
                                  0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F };

            if (mode == "RT") { this.mode = "MRT"; }
            else
            {
                Console.Error.WriteLine("mode not supported\n");
                return;
            }

            result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_MRTMTI, 0, 0, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTConfigure((short)devNum, (RtCmdStkSize)ACE_RT_CMDSTK_2K, (MRtGblDataStkType)ACE_RT_DBLK_GBL_C_128, 50);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            rtAdd = 1;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            for (int i = 1; i < 32; i++)
            {
                Array.Clear(dblk, 0, dblk.Length);
                if (i < 30)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (i == 1) { dblk[j] = bfrs.rtbc_dblk_bc[i - 1]; }
                        else if (j == i - 1) { dblk[j] = bfrs.rtbc_dblk_bc[i - 1]; }
                        else { dblk[j] = bfrs.hex_nums_bc[j]; }
                    }
                }
                else
                {
                    for (int j = 0; j < 32; j++) { dblk[j] = bfrs.rtbc_dblk_alt[j]; }
                }

                result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, (short)(i + 10), (RtDataBlkType)32, dblk, 32);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 50, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_RX, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, (short)(i + 10), (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_TX, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (i < 17)
                {
                    result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), modeData[i - 1]);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
            }

            rtAdd = 10;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 2, (RtDataBlkType)32, dblk, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            for (int i = 1; i < 32; i++)
            {
                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 2, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (i < 17)
                {
                    if (i == 4)
                    {
                        result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), 19);
                        if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    }
                    if (i == 16)
                    {
                        result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)16, 0xde0);
                        if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    }
                    else
                    {
                        result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), modeData[i - 1]);
                        if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    }
                }
            }

            rtAdd = 15;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 3, (RtDataBlkType)32, dblk, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            for (int i = 1; i < 32; i++)
            {
                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 3, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (i < 17)
                {
                    result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), modeData[i - 1]);
                    if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                }
            }

            rtAdd = 21;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 4, (RtDataBlkType)32, dblk, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            for (int i = 1; i < 32; i++)
            {
                result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 4, (ushort)i, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
                if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

                if (i < 17)
                {
                    if (i == 16)
                    {
                        result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)16, 0xde0);
                        if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    }
                    else
                    {
                        result = (short)EmaceBU69092.acexMRTWriteRTModeCodeData((short)devNum, (sbyte)rtAdd, (RtMCData)(ushort)(i + 15), modeData[i - 1]);
                        if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
                    }
                }
            }

            rtAdd = 0;
            result = (short)EmaceBU69092.acexMRTEnableRT((short)devNum, (sbyte)rtAdd, 0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 5, (RtDataBlkType)32, dblk, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
            result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, (sbyte)rtAdd, 5, 1, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTStart((short)devNum, -1, 0);
        }

        public void BCTestRemapSA()
        {
            short result = 0;

            result = (short)EmaceBU69092.acexMRTDataBlkUnmapFromRTSA((short)devNum, 1, 11, 1, (RtMsgType)ACE_RT_MSGTYPE_TX);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTDataBlkUnmapFromRTSA((short)devNum, 1, 50, 1, (RtMsgType)ACE_RT_MSGTYPE_RX);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.aceRTDataBlkCreate((short)devNum, 8, (RtDataBlkType)32, bfrs.rtrt_dblk_bc, 32);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }

            result = (short)EmaceBU69092.acexMRTDataBlkMapToRTSA((short)devNum, 1, 8, 1, (RtMsgType)ACE_RT_MSGTYPE_ALL, (RtDblkIrq)ACE_RT_DBLK_EOM_IRQ, 1>0);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
        }

        public short Start()
        {
            short result;

            if (mode == "BC")
            {
                result = (short)EmaceBU69092.aceBCStart((short)devNum, mjrFrameId, frameMode);
            }
            else if (mode == "RT")
            {
                result = (short)EmaceBU69092.aceRTStart((short)devNum);
            }
            else
            {
                result = (short)EmaceBU69092.aceMTStart((short)devNum);
            }

            if (result != 0)
            {
                Console.WriteLine("aceStart error: " + result);
            }

            return result;
        }

        public short Stop()
        {
            short result;

            if (mode == "BC")
            {
                result = (short)EmaceBU69092.aceBCStop((short)devNum);
            }
            else if (mode == "RT")
            {
                result = (short)EmaceBU69092.aceRTStop((short)devNum);
            }
            else if (mode == "MRT")
            {
                result = (short)EmaceBU69092.acexMRTStop((short)devNum, -1);
                this.mode = "RT";
            }
            else
            {
                result = (short)EmaceBU69092.aceMTStop((short)devNum);
            }

            if (result != 0)
            {
                Console.WriteLine("aceStop error: " + result);
            }

            return result;
        }

        public short Reset()
        {
            short result = 0;

            if (mode == "BC")
            {
                EmaceBU69092.aceBCUninstallHBuf((short)devNum);
                result = (short)EmaceBU69092.aceBCStop((short)devNum);
            }
            else if (mode == "RT")
            {
                result = (short)EmaceBU69092.aceRTStop((short)devNum);
            }
            else if (mode == "MRT")
            {
                EmaceBU69092.acexMRTStop((short)devNum, -1);
                this.mode = "RT";

                for (short i = 1; i < 100; i++)
                {
                    EmaceBU69092.aceRTDataBlkDelete((short)devNum, (ushort)i);
                }
            }
            else
            {
                result = (short)EmaceBU69092.aceMTStop((short)devNum);
            }

            result = (short)EmaceBU69092.aceFree((short)devNum);
            if (result != 0)
            {
                Console.WriteLine("aceFree error: " + result);
            }

            if (mode == "BC")
            {
                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_BC, 0, 0, 0);
            }
            else if (mode == "RT")
            {
                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_RT, 0, 0, 0);
            }
            else
            {
                result = (short)EmaceBU69092.aceInitialize((short)devNum, ACE_ACCESS_CARD, (ConfigMode)ACE_MODE_MT, 0, 0, 0);
            }

            if (result != 0)
            {
                Console.WriteLine("aceInitialize error: " + result);
            }

            frameMode = -1;
            msgId = 1;
            dblkId = 1;
            xeqOpcodeId = 1;
            calOpcodeId = 2;
            mnrFrameId = 1;
            mjrFrameId = 2;
            rtAddr = 1;
            msgType = "BC-RT";

            return result;
        }

        public void Free()
        {
            short result;

            result = (short)EmaceBU69092.aceFree((short)devNum);
            if (result != 0) { Console.Error.WriteLine(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber() + ": " + result); }
        }
    }
}