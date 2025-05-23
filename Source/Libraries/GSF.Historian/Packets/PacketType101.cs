﻿//******************************************************************************************************
//  PacketType101.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  06/04/2009 - Pinal C. Patel
//       Generated original version of source code.
//  09/15/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  09/23/2009 - Pinal C. Patel
//       Edited code comments.
//  10/11/2010 - Mihir Brahmbhatt
//       Updated header and license agreement.
//  11/30/2011 - J. Ritchie Carroll
//       Modified to support buffer optimized ISupportBinaryImage.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GSF.Historian.Files;
using GSF.TimeSeries;

// ReSharper disable VirtualMemberCallInConstructor

namespace GSF.Historian.Packets;

/// <summary>
/// Represents a packet that can be used to send multiple time-series data points to a historian for archival.
/// </summary>
/// <seealso cref="PacketType101DataPoint"/>
public class PacketType101 : PacketBase
{
    // **************************************************************************************************
    // *                                        Binary Structure                                        *
    // **************************************************************************************************
    // * # Of Bytes Byte Index Data Type  Property Name                                                 *
    // * ---------- ---------- ---------- --------------------------------------------------------------*
    // * 2          0-1        Int16      TypeID (packet identifier)                                    *
    // * 4          2-5        Int32      Data.Count                                                    *
    // * 14         6-19       Byte[]     Data[0]                                                       *
    // * 14         n1-n2      Byte[]     Data[Data.Count - 1]                                          *
    // **************************************************************************************************

    #region [ Members ]

    // Fields
    private readonly List<IDataPoint> m_data;

    #endregion

    #region [ Constructors ]

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType101"/> class.
    /// </summary>
    public PacketType101()
        : base(101)
    {
        m_data = [];
        ProcessHandler = Process;
        PreProcessHandler = PreProcess;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType101"/> class.
    /// </summary>
    /// <param name="dataPoints">A collection of time-series data points.</param>
    public PacketType101(IEnumerable<IDataPoint> dataPoints)
        : this()
    {
        if (dataPoints is null)
            throw new ArgumentNullException(nameof(dataPoints));

        foreach (IDataPoint dataPoint in dataPoints)
            m_data.Add(new PacketType101DataPoint(dataPoint));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType101"/> class.
    /// </summary>
    /// <param name="measurements">A collection of measurements.</param>
    public PacketType101(IEnumerable<IMeasurement> measurements)
        : this()
    {
        if (measurements is null)
            throw new ArgumentNullException(nameof(measurements));

        foreach (IMeasurement measurement in measurements)
        {
            m_data.Add(new PacketType101DataPoint(
                (int)measurement.Key.ID,
                new TimeTag((DateTime)measurement.Timestamp),
                (float)measurement.AdjustedValue,
                measurement.HistorianQuality()));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType101"/> class.
    /// </summary>
    /// <param name="buffer">Binary image to be used for initializing <see cref="PacketType101"/>.</param>
    /// <param name="startIndex">0-based starting index of initialization data in the <paramref name="buffer"/>.</param>
    /// <param name="length">Valid number of bytes in <paramref name="buffer"/> from <paramref name="startIndex"/>.</param>
    public PacketType101(byte[] buffer, int startIndex, int length)
        : this()
    {
        ParseBinaryImage(buffer, startIndex, length);
    }

    #endregion

    #region [ Properties ]

    /// <summary>
    /// Gets the time-series data in <see cref="PacketType101"/>.
    /// </summary>
    public IList<IDataPoint> Data => m_data;

    /// <summary>
    /// Gets the length of the <see cref="PacketType101"/>.
    /// </summary>
    public override int BinaryLength => 2 + 4 + m_data.Count * PacketType101DataPoint.FixedLength;

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Initializes <see cref="PacketType101"/> from the specified <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Binary image to be used for initializing <see cref="PacketType101"/>.</param>
    /// <param name="startIndex">0-based starting index of initialization data in the <paramref name="buffer"/>.</param>
    /// <param name="length">Valid number of bytes in <paramref name="buffer"/> from <paramref name="startIndex"/>.</param>
    /// <returns>Number of bytes used from the <paramref name="buffer"/> for initializing <see cref="PacketType101"/>.</returns>
    public override int ParseBinaryImage(byte[] buffer, int startIndex, int length)
    {
        // Binary image does not have sufficient data.
        if (length < 6)
            return 0;
        
        // Binary image has sufficient data.
        short packetID = LittleEndian.ToInt16(buffer, startIndex);
        
        if (packetID != TypeID)
            throw new ArgumentException($"Unexpected packet id '{packetID}' (expected '{TypeID}')");

        // Ensure that the binary image is complete
        int dataCount = LittleEndian.ToInt32(buffer, startIndex + 2);
        
        if (length < 6 + dataCount * PacketType101DataPoint.FixedLength)
            return 0;

        // We have a binary image with the correct packet id.
        int offset;
        
        m_data.Clear();
        
        for (int i = 0; i < dataCount; i++)
        {
            offset = startIndex + 6 + i * PacketType101DataPoint.FixedLength;
            m_data.Add(new PacketType101DataPoint(buffer, offset, length - offset));
        }

        return BinaryLength;
    }

    /// <summary>
    /// Generates binary image of the <see cref="PacketType101"/> and copies it into the given buffer, for <see cref="BinaryLength"/> bytes.
    /// </summary>
    /// <param name="buffer">Buffer used to hold generated binary image of the source object.</param>
    /// <param name="startIndex">0-based starting index in the <paramref name="buffer"/> to start writing.</param>
    /// <returns>The number of bytes written to the <paramref name="buffer"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> or <see cref="BinaryLength"/> is less than 0 -or- 
    /// <paramref name="startIndex"/> and <see cref="BinaryLength"/> will exceed <paramref name="buffer"/> length.
    /// </exception>
    public override int GenerateBinaryImage(byte[] buffer, int startIndex)
    {
        int length = BinaryLength;

        buffer.ValidateParameters(startIndex, length);

        Buffer.BlockCopy(LittleEndian.GetBytes(TypeID), 0, buffer, startIndex, 2);
        Buffer.BlockCopy(LittleEndian.GetBytes(m_data.Count), 0, buffer, startIndex + 2, 4);

        for (int i = 0; i < m_data.Count; i++)
            m_data[i].GenerateBinaryImage(buffer, startIndex + 6 + i * PacketType101DataPoint.FixedLength);

        return length;
    }

    /// <summary>
    /// Extracts time-series data from <see cref="PacketType101"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> object of <see cref="ArchiveDataPoint"/>s.</returns>
    public override IEnumerable<IDataPoint> ExtractTimeSeriesData()
    {
        return m_data.Select(dataPoint => new ArchiveDataPoint(dataPoint));
    }

    /// <summary>
    /// Processes <see cref="PacketType101"/>.
    /// </summary>
    /// <returns>A null reference.</returns>
    protected virtual IEnumerable<byte[]> Process()
    {
        if (Archive is null)
            return null;
        
        foreach (IDataPoint dataPoint in ExtractTimeSeriesData())
            Archive.WriteData(dataPoint);

        return null;
    }

    /// <summary>
    /// Pre-processes <see cref="PacketType101"/>.
    /// </summary>
    /// <returns>A <see cref="byte"/> array for "ACK".</returns>
    protected virtual IEnumerable<byte[]> PreProcess()
    {
        return [Encoding.ASCII.GetBytes("ACK")];
    }

    #endregion
}