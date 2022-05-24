﻿using NARCSharp.Sections;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NARCSharp {
    public class NARC {
        /// <summary>
        /// A shortcut to <see cref="Files"/>.
        /// </summary>
        public byte[] this[string key] { get => Files[key]; set => Files[key] = value; }

        /// <summary>
        /// The byte order of the <see cref="NARC"/> file, often being <see cref="ByteOrder.LittleEndian"/>.
        /// </summary>
        public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
        /// <summary>
        /// The version of the <see cref="NARC"/> file, often being '1'.
        /// </summary>
        public ushort Version { get; set; } = 1;
        /// <summary>
        /// Contains all the files inside the <see cref="NARC"/> file.
        /// </summary>
        public Dictionary<string, byte[]> Files { get; set; } = new Dictionary<string, byte[]>();

        // private (for proper writing)
        private readonly ushort headerLength = 16;
        private readonly ushort sectionCount = 3;
        private readonly ulong btnfUnknown = 281474976710664;

        /// <summary>
        /// Creates a new NARC.
        /// </summary>
        public NARC() { }

        /// <summary>
        /// Reads a <see cref="NARC"/> from a byte array.
        /// </summary>
        /// <param name="bytes"></param>
        public NARC(byte[] bytes) : this(new MemoryStream(bytes)) { }

        /// <summary>
        /// Reads a <see cref="NARC"/> from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="leaveOpen">Whether or not the stream will be kept opened. (false for disposing it)</param>
        public NARC(Stream stream, bool leaveOpen = false) {
            using BinaryDataReader reader = new(stream, leaveOpen);

            // Magic check:
            if(Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpperInvariant() != "NARC")
                throw new InvalidDataException("The given file is not a NARC.");

            // Read the byte order and changing the one used by the reader if needed:
            ByteOrder = (ByteOrder) reader.ReadUInt16();
            reader.ByteOrder = ByteOrder;

            Version = reader.ReadUInt16();
            uint length = reader.ReadUInt32();

            headerLength = reader.ReadUInt16(); // Useless.
            sectionCount = reader.ReadUInt16();

            // Buffers
            BFAT bfat = new();
            BFNT bfnt = new();
            FIMG fimg = new();

            // Get all the sections and writes them to the buffers:
            for(uint i = 0; i < sectionCount; i++) {
                uint cSectionSize;
                using(reader.TemporarySeek()) {
                    string cSectionMagic = Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpperInvariant();
                    cSectionSize = reader.ReadUInt32();
#if DEBUG
                    Console.WriteLine(cSectionMagic);
#endif
                    switch(cSectionMagic) {
                        case "BTAF":
                            bfat = new(reader);
                            break;
                        case "BTNF":
                            bfnt = new(reader, (uint) bfat.FileDataArray.Length);
                            break;
                        case "GMIF":
                            fimg = new(reader, bfat);
                            break;
                    }
                }

                reader.Position += cSectionSize;
            }

            // Parse the buffers into objects:
            for(int i = 0; i < bfnt.FileNames.Length; i++)
                Files.Add(bfnt.FileNames[i], fimg.FilesData[i]);

            // Set unknown value (for proper writing):
            btnfUnknown = btnf.Unknown;
        }

        /// <returns>A packed <see cref="NARC"/> as a byte array.</returns>
        public byte[] Write() {
            using MemoryStream stream = new();

            Write(stream, true);
            return stream.ToArray();

        }

        /// <summary>
        /// Writes the <see cref="NARC"/> to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="leaveOpen">Whether or not the stream will be kept opened. (false for disposing it)</param>
        public void Write(Stream stream, bool leaveOpen = false) {
            using BinaryDataWriter writer = new(stream, leaveOpen);

            #region Header
            writer.Write("NARC", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII); // Magic.

            writer.ByteOrder = ByteOrder;
            writer.Write((ushort) 0xFFFE); // ByteOrder.

            writer.Write(Version); // Version.

            long headerLengthPosition = writer.Position;
            writer.WriteBytes(4, 0x00); // Skips length writing.

            writer.Write(headerLength); // Header length.
            writer.Write(sectionCount); // Section count.
            #endregion

            #region BFAT preparation
            long bfatPosition = WriteSectionHeader("BTAF"); // Header.
            writer.Write((uint) Files.Count); // File count.

            for(uint i = 0; i < Files.Count; i++) // Reads unset bytes per file. (Reserved space)
                writer.WriteBytes(8, 0x00);

            WriteSectionLength(bfatPosition);
            #endregion

            writer.Write(btnfUnknown);
            #region BFNT
            long bfntPosition = WriteSectionHeader("BTNF"); // Header.

            foreach(string name in Files.Keys)
                writer.Write(name, BinaryStringFormat.ByteLengthPrefix);
            writer.Write((byte) 0x00);

            writer.Align(32);
            writer.Position += 8;

            WriteSectionLength(bfntPosition);
            #endregion

            #region FIMG
            long fimgPosition = WriteSectionHeader("GMIF"); // Header.

            long bfatCurrentPosition = bfatPosition + 12; // First offset-size position. (BFAT)
            foreach(byte[] file in Files.Values) {
                WriteBFATEntry(); // BFAT offset
                writer.Write(file);
                WriteBFATEntry(); // BFAT size.
                writer.Align(16);
            }

            WriteSectionLength(fimgPosition);
            #endregion

            writer.Position = headerLengthPosition;
            writer.Write((uint) writer.BaseStream.Length); // Total file length.


            long WriteSectionHeader(string magic) {
                long startPosition = writer.Position;
                writer.Write(magic, BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII); // Magic.

                writer.WriteBytes(4, 0x00); // Skips length position.

                return startPosition;
            }

            void WriteSectionLength(long startPosition) {
                using(writer.TemporarySeek()) {
                    long finalLength = (uint) writer.Position;

                    writer.Position = startPosition + 4;
                    writer.Write((uint) (finalLength - startPosition));
                }
            }

            void WriteBFATEntry() {
                uint value = (uint) (writer.Position - (fimgPosition + 8));

                using(writer.TemporarySeek()) {
                    writer.Position = bfatCurrentPosition;
                    writer.Write(value);
                }

                bfatCurrentPosition += 4;
            }
        }
    }
}
