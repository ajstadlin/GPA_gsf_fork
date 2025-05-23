//******************************************************************************************************
//  PacketType11.cs - Gbtc
//
//  Copyright � 2012, Grid Protection Alliance.  All Rights Reserved.
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
//  06/18/2007 - Pinal C. Patel
//       Generated original version of source code.
//  04/21/2009 - Pinal C. Patel
//       Converted to C#.
//  09/10/2009 - Pinal C. Patel
//       Modified Process() to use deferred execution using "yield" for efficiency.
//  09/15/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  10/11/2010 - Mihir Brahmbhatt
//       Updated header and license agreement.
//  11/30/2011 - J. Ritchie Carroll
//       Modified to support buffer optimized ISupportBinaryImage.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System.Collections.Generic;
using GSF.Historian.Files;
using GSF.Parsing;

// ReSharper disable VirtualMemberCallInConstructor

namespace GSF.Historian.Packets;

/// <summary>
/// Represents a packet to be used for requesting <see cref="StateRecord.Summary"/> for the <see cref="QueryPacketBase.RequestIDs"/>.
/// </summary>
public class PacketType11 : QueryPacketBase
{
    #region [ Constructors ]

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType11"/> class.
    /// </summary>
    public PacketType11()
        : base(11)
    {
        ProcessHandler = Process;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketType11"/> class.
    /// </summary>
    /// <param name="buffer">Binary image to be used for initializing <see cref="PacketType11"/>.</param>
    /// <param name="startIndex">0-based starting index of initialization data in the <paramref name="buffer"/>.</param>
    /// <param name="length">Valid number of bytes in <paramref name="buffer"/> from <paramref name="startIndex"/>.</param>
    public PacketType11(byte[] buffer, int startIndex, int length)
        : this()
    {
        ParseBinaryImage(buffer, startIndex, length);
    }

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Processes <see cref="PacketType11"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> object containing the binary images of <see cref="StateRecord.Summary"/> for the <see cref="QueryPacketBase.RequestIDs"/>.</returns>
    protected virtual IEnumerable<byte[]> Process()
    {
        if (Archive is null)
            yield break;

        byte[] data;
        
        if (RequestIDs.Count == 0 || (RequestIDs.Count == 1 && RequestIDs[0] == -1))
        {
            // Information for all defined records is requested.
            int id = 0;
            
            while (true)
            {
                data = Archive.ReadStateDataSummary(++id);
                
                if (data is null)
                    break; // No more records.

                // Yield retrieved data.
                yield return data;
            }
        }
        else
        {
            // Information for specific records is requested.
            foreach (int id in RequestIDs)
            {
                data = Archive.ReadStateDataSummary(id);
                
                if (data is null)
                    continue; // ID is invalid.
                
                // Yield retrieved data.
                yield return data;
            }
        }

        yield return new StateRecord(-1).Summary.BinaryImage();   // To indicate EOT.
    }

    #endregion
}