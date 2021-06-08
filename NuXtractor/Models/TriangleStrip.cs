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

using NuXtractor.Materials;

using System.Threading.Tasks;

namespace NuXtractor.Models
{
    public abstract class TriangleStrip : Model
    {
        public TriangleStrip(Material material) : base(material)
        {
        }

        public abstract Task<int[]> GetIndiciesAsync();

        public override async Task<Triangle[]> GetTrianglesAsync()
        {
            var arr = await GetIndiciesAsync();

            Triangle[] strip = new Triangle[arr.Length - 2];
            for (int i = 0; i < strip.Length; i++)
            {
                strip[i] = new Triangle(arr[i + i % 2], arr[i - i % 2 + 1], arr[i + 2]);
            }

            return strip;
        }
    }
}