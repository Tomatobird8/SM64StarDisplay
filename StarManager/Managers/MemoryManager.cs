﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using LiveSplit.ComponentUtil;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace StarDisplay
{
    public class MemoryManager
    {
        public readonly Process Process;
        public LayoutDescription ld;
        public ROMManager rm;
        MagicManager mm;

        int previousTime;
        byte[] oldStars;
        public byte[] highlightPivot { get; private set; }

        byte[] defPicture;

        IntPtr igt;
        public IntPtr[] files;

        IntPtr levelPtr;
        IntPtr areaPtr;
        IntPtr starPtr;
        IntPtr redsPtr;

        IntPtr segmentsTablePtr;
        IntPtr selectedStarPtr;

        IntPtr romPtr;
        IntPtr romCRCPtr;

        private int[] courseLevels = { 0, 9, 24, 12, 5, 4, 7, 22, 8, 23, 10, 11, 36, 13, 14, 15 };
        private int[] secretLevels = { 0, 17, 19, 21, 27, 28, 29, 18, 31, 20, 25 };
        private int[] overworldLevels = { 6, 26, 16 };

        public int selectedFile;

        public MemoryManager(Process process, LayoutDescription ld, GraphicsManager gm, ROMManager rm, byte[] highlightPivot)
        {
            this.Process = process;
            this.ld = ld;
            this.rm = rm;
            this.highlightPivot = highlightPivot;
            oldStars = new byte[32];

            defPicture = File.ReadAllBytes("images/star.rgba16");
        }

        public bool ProcessActive()
        {
            return Process == null || Process.HasExited;
        }

        public bool isMagicDone()
        {
            return mm != null && mm.isValid();
        }

        public void doMagic()
        {
            List<int> romPtrBaseSuggestions = new List<int>();
            List<int> ramPtrBaseSuggestions = new List<int>();

            DeepPointer[] ramPtrBaseSuggestionsDPtrs = { new DeepPointer("Project64.exe", 0xD6A1C),     //1.6
                    new DeepPointer("RSP 1.7.dll", 0x4C054), new DeepPointer("RSP 1.7.dll", 0x44B5C)        //2.3.2; 2.4
                };

            DeepPointer[] romPtrBaseSuggestionsDPtrs = { new DeepPointer("Project64.exe", 0xD6A2C),     //1.6
                    new DeepPointer("RSP 1.7.dll", 0x4C050), new DeepPointer("RSP 1.7.dll", 0x44B58)        //2.3.2; 2.4
                };

            // Time to generate some addesses for magic check
            foreach (DeepPointer romSuggestionPtr in romPtrBaseSuggestionsDPtrs)
            {
                int ptr = -1;
                try
                {
                    ptr = romSuggestionPtr.Deref<int>(Process);
                    romPtrBaseSuggestions.Add(ptr);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            foreach (DeepPointer ramSuggestionPtr in ramPtrBaseSuggestionsDPtrs)
            {
                int ptr = -1;
                try
                {
                    ptr = ramSuggestionPtr.Deref<int>(Process);
                    ramPtrBaseSuggestions.Add(ptr);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            mm = new MagicManager(Process, romPtrBaseSuggestions.ToArray(), ramPtrBaseSuggestions.ToArray());

            igt = new IntPtr(mm.ramPtrBase + 0x32D580);
            files = new IntPtr[4];
            files[0] = new IntPtr(mm.ramPtrBase + 0x207708);
            files[1] = new IntPtr(mm.ramPtrBase + 0x207778);
            files[2] = new IntPtr(mm.ramPtrBase + 0x2077E8);
            files[3] = new IntPtr(mm.ramPtrBase + 0x207858);

            levelPtr = new IntPtr(mm.ramPtrBase + 0x32DDFA);
            areaPtr = new IntPtr(mm.ramPtrBase + 0x33B249);
            starPtr = new IntPtr(mm.ramPtrBase + 0x064F80 + 0x04800);
            redsPtr = new IntPtr(mm.ramPtrBase + 0x3613FD);

            segmentsTablePtr = new IntPtr(mm.ramPtrBase + 0x33B400);
            selectedStarPtr = new IntPtr(mm.ramPtrBase + 0x1A81A3);

            romPtr = new IntPtr(mm.romPtrBase + 0);
            romCRCPtr = new IntPtr(mm.romPtrBase + 0x10);
        }

        public void DeleteStars()
        {
            int curTime = Process.ReadValue<int>(igt);
            if (curTime > 200 || curTime < 60) return;

            previousTime = curTime;
            byte[] data = Enumerable.Repeat((byte)0x00, 0x70).ToArray();
            IntPtr file = files[selectedFile];
            if (!Process.WriteBytes(file, data))
            {
                throw new IOException();
            }
        }

        public string GetROMName()
        {
            return rm.GetROMName();
        }

        public byte GetCurrentStar()
        {
            if (selectedStarPtr == null) return 0;
            return Process.ReadValue<byte>(selectedStarPtr);
        }

        public byte GetCurrentArea()
        {
            if (areaPtr == null) return 0;
            return Process.ReadValue<byte>(areaPtr);
        }

        private int GetCurrentOffset()
        {
            int level = Process.ReadValue<byte>(levelPtr);
            if (level == 0) return -1;
            int courseLevel = Array.FindIndex(courseLevels, lvl => lvl == level);
            if (courseLevel != -1) return courseLevel + 3;
            int secretLevel = Array.FindIndex(secretLevels, lvl => lvl == level);
            if (secretLevel != -1) return secretLevel + 18;
            int owLevel = Array.FindIndex(overworldLevels, lvl => lvl == level);
            if (owLevel != -1) return 0;
            return -2;
        }

        public TextHighlightAction GetCurrentLineAction()
        {
            int offset = GetCurrentOffset();

            int courseIndex = Array.FindIndex(ld.courseDescription, lind => lind != null && !lind.isTextOnly && lind.offset == offset);
            if (courseIndex != -1) return new TextHighlightAction(courseIndex, false, ld.courseDescription[courseIndex].text);
            int secretIndex = Array.FindIndex(ld.secretDescription, lind => lind != null && !lind.isTextOnly && lind.offset == offset);
            if (secretIndex != -1) return new TextHighlightAction(secretIndex, true, ld.secretDescription[secretIndex].text);

            return null;
        }

        static public int countStars(byte stars)
        {
            int answer = 0;
            for (int i = 1; i <= 7; i++)
                answer += ((stars & (1 << (i - 1))) == 0) ? 0 : 1;
            return answer;
        }

        public sbyte GetReds()
        {
            return Process.ReadValue<sbyte>(redsPtr);
        }

        public int GetSecrets()
        {
            return SearchObjects(0x800EF0B4);
        }

        public int GetActivePanels()
        {
            return SearchObjects(0x800EB770, 1) + SearchObjects(0x800EB770, 2); //1 - active, 2 - finalized
        }

        public int GetAllPanels()
        {
            return SearchObjects(0x800EB770);
        }

        public Bitmap GetImage()
        {
            byte[] data = Process.ReadBytes(starPtr, 512);

            for (int i = 0; i < 512; i += 4) //TODO: Better ending convert
            {
                byte[] copy = new byte[4];
                copy[0] = data[i + 0];
                copy[1] = data[i + 1];
                copy[2] = data[i + 2];
                copy[3] = data[i + 3];
                data[i + 0] = copy[3];
                data[i + 1] = copy[2];
                data[i + 2] = copy[1];
                data[i + 3] = copy[0];
            }
            return FromRGBA16(data);
        }

        public byte[] GetROM()
        {
            int[] romSizesMB = new int[] { 64, 48, 32, 24, 16, 8 };
            byte[] rom = null;
            int romSize = 0;
            foreach (int sizeMB in romSizesMB)
            {
                romSize = 1024 * 1024 * sizeMB;
                rom = Process.ReadBytes(romPtr, romSize);
                if (rom != null) break;
            }
            if (rom == null) return null;
            for (int i = 0; i < romSize; i += 4)
            {
                Array.Reverse(rom, i, 4);
            }
            return rom;
        }

        public UInt16 GetRomCRC()
        {
            return Process.ReadValue<UInt16>(romCRCPtr);
        }

        public byte[] GetStars()
        {
            int length = 32;
            IntPtr file = files[selectedFile];

            byte[] stars = Process.ReadBytes(file, length);
            if (stars == null) return null;

            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            return stars;
        }

        public static Bitmap FromRGBA16(byte[] data)
        {
            Bitmap picture = new Bitmap(16, 16);
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    int offset = (16 * j + i) * 2;
                    int colorARGB = (data[offset + 1] & 0x01) * 255 << 24
                        | (data[offset] & 0xF8) << 16 | (data[offset] & 0xE0) << 11
                        | (data[offset] & 0x07) << 13 | (data[offset] & 0x07) << 8
                        | (data[offset + 1] & 0xC0) << 5
                        | (data[offset + 1] & 0x3E) << 2 | (data[offset + 1] & 0x38) >> 3;

                    Color c = Color.FromArgb(colorARGB);
                    picture.SetPixel(i, j, c);
                }
            }
            return picture;
        }

        public void resetHighlightPivot()
        {
            highlightPivot = null;
        }

        public DrawActions GetDrawActions()
        {
            int length = 32;
            IntPtr file = files[selectedFile];
            byte[] stars = Process.ReadBytes(file, length);
            if (stars == null) return null;

            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            if (highlightPivot == null)
                highlightPivot = stars;

            int totalReds = 0, reds = 0;
            try
            {
                totalReds = rm != null ? rm.ParseReds(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                reds = GetReds();
            }
            catch (Exception) {  }
            if (totalReds != 0) //Fix reds amount -- intended total amount is 8, so we should switch maximum to totalReds
                reds += totalReds - 8;
            else //If we got any reds we might not be able to read total amount properly, so we set total amount to current reds to display only them
                totalReds = reds;


            //Operations are the same as with regular reds
            int totalSecrets = 0, secrets = 0;
            try
            {
                totalSecrets = rm != null ? rm.ParseSecrets(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                secrets = totalSecrets - GetSecrets();
            }catch(Exception) { }

            //Operations are the same as with regular reds
            int totalPanels = 0, activePanels = 0;
            try
            {
                totalPanels = rm != null ? rm.ParseFlipswitches(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                activePanels = GetActivePanels();
            }
            catch (Exception) { }

            DrawActions da = new DrawActions(ld, stars, oldStars, highlightPivot, reds, totalReds, secrets, totalSecrets, activePanels, totalPanels);
            oldStars = stars;
            return da;
        }

        public DrawActions GetCollectablesOnlyDrawActions()
        {
            int length = 32;
            IntPtr file = files[selectedFile];
            byte[] stars = Process.ReadBytes(file, length);
            if (stars == null) return null;

            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            if (highlightPivot == null)
                highlightPivot = stars;

            int totalReds = 0, reds = 0;
            try
            {
                totalReds = rm != null ? rm.ParseReds(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                reds = GetReds();
            }
            catch (Exception) { }
            if (totalReds != 0) //Fix reds amount -- intended total amount is 8, so we should switch maximum to totalReds
                reds += totalReds - 8;
            else //If we got any reds we might not be able to read total amount properly, so we set total amount to current reds to display only them
                totalReds = reds;


            //Operations are the same as with regular reds
            int totalSecrets = 0, secrets = 0;
            try
            {
                totalSecrets = rm != null ? rm.ParseSecrets(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                secrets = totalSecrets - GetSecrets();
            }
            catch (Exception) { }

            //Operations are the same as with regular reds
            int totalPanels = 0, activePanels = 0;
            try
            {
                totalPanels = rm != null ? rm.ParseFlipswitches(ld, GetCurrentLineAction(), GetCurrentStar(), GetCurrentArea()) : 0;
                activePanels = GetActivePanels();
            }
            catch (Exception) { }

            DrawActions da = new CollectablesOnlyDrawActions(ld, stars, oldStars, highlightPivot, reds, totalReds, secrets, totalSecrets, activePanels, totalPanels);
            oldStars = stars;
            return da;
        }

        public int SearchObjects(UInt32 searchBehaviour)
        {
            int count = 0;

            UInt32 address = 0x33D488;
            do
            {
                DeepPointer currentObject = new DeepPointer(mm.ramPtrBase + (int)address);
                byte[] data = currentObject.DerefBytes(Process, 0x260);

                UInt32 intparam = BitConverter.ToUInt32(data, 0x180);
                UInt32 behaviourActive1 = BitConverter.ToUInt32(data, 0x1CC);
                UInt32 behaviourActive2 = BitConverter.ToUInt32(data, 0x1D0);
                UInt32 initialBehaviour = BitConverter.ToUInt32(data, 0x20C);
                UInt32 scriptParameter = BitConverter.ToUInt32(data, 0x0F0);

                //Console.Write("{0:X8}({1:X8}) ", behaviourActive1, scriptParameter);

                if (behaviourActive1 == searchBehaviour)
                {
                    count++;
                }

                address = BitConverter.ToUInt32(data, 0x8) & 0x7FFFFFFF;
            } while (address != 0x33D488 && address != 0);
            //Console.WriteLine();
            return count;
        }

        public int SearchObjects(UInt32 searchBehaviour, UInt32 state)
        {
            int count = 0;

            UInt32 address = 0x33D488;
            do
            {
                DeepPointer currentObject = new DeepPointer(mm.ramPtrBase + (int)address);
                byte[] data = currentObject.DerefBytes(Process, 0x260);

                UInt32 intparam = BitConverter.ToUInt32(data, 0x180);
                UInt32 behaviourActive1 = BitConverter.ToUInt32(data, 0x1CC);
                UInt32 behaviourActive2 = BitConverter.ToUInt32(data, 0x1D0);
                UInt32 initialBehaviour = BitConverter.ToUInt32(data, 0x20C);
                UInt32 scriptParameter = BitConverter.ToUInt32(data, 0x0F0);

                //Console.Write("{0:X8}({1:X8}) ", behaviourActive1, scriptParameter);

                if (behaviourActive1 == searchBehaviour && scriptParameter == state)
                {
                    count++;
                }

                address = BitConverter.ToUInt32(data, 0x8) & 0x7FFFFFFF;
            } while (address != 0x33D488 && address != 0);
            //Console.WriteLine();
            return count;
        }

        public void InvalidateCache()
        {
            oldStars = new byte[32];
        }

        public void WriteToFile(int offset, int bit)
        {
            int length = 32;
            IntPtr file = files[selectedFile];

            byte[] stars = Process.ReadBytes(file, length);
            if (stars == null) return;

            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            //fix stuff here!!!
            stars[offset] = (byte) (stars[offset] ^ (byte)(1 << bit)); //???

            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            if (!Process.WriteBytes(file, stars))
            {
                MessageBox.Show("Can't edit files!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void WriteToFile(byte[] data)
        {
            int length = 32;
            IntPtr file = files[selectedFile];

            byte[] stars = data;
            if (stars == null) return;
            
            for (int i = 0; i < length; i += 4)
                Array.Reverse(stars, i, 4);

            if (!Process.WriteBytes(file, stars))
            {
                MessageBox.Show("Can't edit files!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public string GetTitle()
        {
            Process.Refresh();
            return Process.MainWindowTitle;
        }
    }
}