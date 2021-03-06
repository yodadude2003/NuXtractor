﻿/*
 *  Copyright 2020 Chosen Few Software
 *  This file is part of NuXtractor.
 *
 *  NuXtractor is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  NuXtractor is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with NuXtractor.  If not, see <https://www.gnu.org/licenses/>.
 */

using MightyStruct;
using MightyStruct.Serializers;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuXtractor.Textures.DXT
{
    public class DXT1Texture : Texture
    {
        public Endianness Endianness { get; }

        protected ISerializer<ushort> Words { get; }
        protected PixelBlender<RgbaVector> Pixels { get; }

        public DXT1Texture(int id, int width, int height, int levels, Endianness endianness, Stream stream) : base(id, width, height, levels, stream)
        {
            Endianness = endianness;

            Words = new UInt16Serializer(Endianness);
            Pixels = PixelOperations<RgbaVector>.Instance
                .GetPixelBlender(new GraphicsOptions());
        }

        protected ushort ConvertColorToBits(RgbaVector color)
        {
            int r = (int)(color.R * 31.0f);
            int g = (int)(color.G * 63.0f);
            int b = (int)(color.B * 31.0f);

            return (ushort)((r << 11) | (g << 5) | (b));
        }

        protected async Task<RgbaVector> ReadColorAsync()
        {
            ushort color = await Words.ReadFromStreamAsync(Stream);

            float r = (color >> 11) / 31.0f;
            float g = ((color & 0x7E0) >> 5) / 63.0f;
            float b = (color & 0x1F) / 31.0f;

            return new RgbaVector(r, g, b);
        }

        protected async Task<RgbaVector[]> ReadPaletteAsync()
        {
            var colors = new List<RgbaVector>
            {
                await ReadColorAsync(),
                await ReadColorAsync()
            };

            if (ConvertColorToBits(colors[0]) > ConvertColorToBits(colors[1]))
            {
                colors.Add(Pixels.Blend(colors[0], colors[1], 1.0f / 3.0f));
                colors.Add(Pixels.Blend(colors[1], colors[0], 1.0f / 3.0f));
            }
            else
            {
                colors.Add(Pixels.Blend(colors[0], colors[1], 0.5f));
                colors.Add(new RgbaVector(0.0f, 0.0f, 0.0f, 0.0f));
            }

            return colors.ToArray();
        }

        protected virtual async Task<RgbaVector[]> ReadTileAsync()
        {
            RgbaVector[] colors = await ReadPaletteAsync();

            byte[] buffer = new byte[4];
            await Stream.ReadAsync(buffer, 0, buffer.Length);


            return buffer
                .SelectMany(r => new int[] { (r & 0xC0) >> 6, (r & 0x30) >> 4, (r & 0x0C) >> 2, (r & 0x03) })
                .Select(i => colors[i])
                .ToArray();
        }

        public override async Task<Image<RgbaVector>> ReadImageAsync()
        {
            Stream.Seek(0, SeekOrigin.Begin);

            var image = new Image<RgbaVector>(Width, Height);
            for (int y = 0; y < Height; y += 4)
            {
                for (int x = 0; x < Width; x += 4)
                {
                    var tile = await ReadTileAsync();
                    for (int i = 0; i < 16; i++)
                    {
                        image[x + (3 - (i % 4)), y + (i / 4)] = tile[i];
                    }
                }
            }

            return image;
        }

        protected (float lo, float hi) CalcBestExtremes(float[] fValues)
        {
            if (fValues.Length == 1)
            {
                return (fValues[0], fValues[0]);
            }
            else if (fValues.Length == 2)
            {
                return (fValues[0], fValues[1]);
            }

            float N = fValues.Length;

            float sumX = 0, sumY = 0;
            float sumXY = 0, sumXSq = 0;

            for (int j = 0; j < fValues.Length; j++)
            {
                sumX += j;
                sumY += fValues[j];

                sumXY += j * fValues[j];
                sumXSq += j * j;
            }


            // line of best fit: y = mx + b
            float m = (N * sumXY - sumX * sumY) / (N * sumXSq - sumX * sumX);
            float b = sumY / N - m * sumX / N;

            float lo = Math.Clamp(b, 0.0f, 1.0f);
            float hi = Math.Clamp((N - 1) * m + lo, 0.0f, 1.0f);

            return (lo, hi);
        }

        private RgbaVector[] CalcTilePalette(RgbaVector[] tile)
        {
            var filtered = tile.Where(c => c.A > 0.0f);

            float[] reds = filtered.Select(c => c.R).ToArray();
            float[] greens = filtered.Select(c => c.G).ToArray();
            float[] blues = filtered.Select(c => c.B).ToArray();

            float[] channelRes = new float[] { 31.0f, 63.0f, 31.0f };

            float[][] channels = new float[][] { reds, greens, blues };

            (float lo, float hi)[] results = new (float lo, float hi)[channels.Length];

            for (int i = 0; i < channels.Length; i++)
            {
                var fValues = channels[i]
                    .Select(n => (float)Math.Floor(n * channelRes[i] + 0.5f) / channelRes[i])
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();

                results[i] = CalcBestExtremes(fValues);
            }

            var red = results[0];
            var green = results[1];
            var blue = results[2];

            var loColor = new RgbaVector(red.lo, green.lo, blue.lo);
            var hiColor = new RgbaVector(red.hi, green.hi, blue.hi);

            if (tile.Any(c => c.A == 0.0f))
            {
                if (ConvertColorToBits(loColor) > ConvertColorToBits(hiColor))
                {
                    var temp = loColor;
                    loColor = hiColor;
                    hiColor = temp;
                }

                var midColor = Pixels.Blend(loColor, hiColor, 0.5f);
                return new RgbaVector[] { loColor, hiColor, midColor };
            }
            else
            {
                if (ConvertColorToBits(loColor) <= ConvertColorToBits(hiColor))
                {
                    var temp = loColor;
                    loColor = hiColor;
                    hiColor = temp;
                }

                var midColor1 = Pixels.Blend(loColor, hiColor, 1.0f / 3.0f);
                var midColor2 = Pixels.Blend(hiColor, loColor, 1.0f / 3.0f);

                return new RgbaVector[] { loColor, hiColor, midColor1, midColor2 };
            }
        }

        private int[] CalcTileIndicies(RgbaVector[] palette, RgbaVector[] tile)
        {
            return tile
                .Select(c =>
                    c.A > 0.0f ?
                   palette.Select(
                        (p, i) =>
                            (sc: (Math.Abs(c.R - p.R) + Math.Abs(c.G - p.G) + Math.Abs(c.B - p.B)) / 3, i)
                            )
                    .OrderBy(x => x.sc).First().i : 3)
                .ToArray();
        }

        private Task WriteColorAsync(RgbaVector color)
        {
            return Words.WriteToStreamAsync(Stream, ConvertColorToBits(color));
        }

        protected virtual async Task WriteTileAsync(RgbaVector[] tile)
        {
            RgbaVector[] palette = CalcTilePalette(tile);

            await WriteColorAsync(palette[0]);
            await WriteColorAsync(palette[1]);

            int[] indicies = CalcTileIndicies(palette, tile);

            byte[] buffer = new byte[4];
            for (int i = 0; i < 16; i += 4)
            {
                buffer[i / 4] = (byte)((indicies[i + 0] << 6) | (indicies[i + 1] << 4) | (indicies[i + 2] << 2) | (indicies[i + 3]));
            }

            await Stream.WriteAsync(buffer, 0, buffer.Length);
        }

        public override async Task WriteImageAsync(Image<RgbaVector> image)
        {
            Stream.Seek(0, SeekOrigin.Begin);

            for (int l = 0; l <= Levels; l++)
            {
                for (int y = 0; y < Height >> l; y += 4)
                {
                    for (int x = 0; x < Width >> l; x += 4)
                    {
                        var tile = new RgbaVector[16];
                        for (int i = 0; i < 16; i++)
                        {
                            int tx = (x + (3 - (i % 4))) << l;
                            int ty = (y + (i / 4)) << l;
                            if (tx < Width && ty < Height)
                            {
                                tile[i] = image[tx, ty];
                            }
                        }
                        await WriteTileAsync(tile);
                    }
                }
            }
        }
    }
}
