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

using Rainbow.ImgLib.Common;
using Rainbow.ImgLib.Encoding;
using Rainbow.ImgLib.Encoding.Implementation;
using SkiaSharp;
using System;

namespace NuXtractor.Textures
{
    public class GCTexture : Texture
    {
        private ushort Type { get; }

        public GCTexture(int width, int height, byte[] data, ushort type) : base(width, height, data)
        {
            Type = type;
        }

        public override SKBitmap ToBitmap()
        {
            throw new NotImplementedException("Gamecube textures cannot be converted. Use Dump mode instead.");
        }
    }
}