﻿//******************************************************************************************************
//  LogicalParser.cs - Gbtc
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
//  05/03/2012 - Stephen C. Wills, Grid Protection Alliance
//       Generated original version of source code.
//  12/17/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.IO;
using System.Collections.Generic;
using Gemstone.PQDIF.Physical;

namespace Gemstone.PQDIF.Logical
{
    /// <summary>
    /// Represents a parser which parses the logical structure of a PQDIF file.
    /// </summary>
    public class LogicalParser : IDisposable
    {
        #region [ Members ]

        // Fields
        private readonly PhysicalParser m_physicalParser;
        private DataSourceRecord? m_currentDataSourceRecord;
        private MonitorSettingsRecord? m_currentMonitorSettingsRecord;
        private ObservationRecord? m_nextObservationRecord;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="LogicalParser"/> class.
        /// </summary>
        /// <param name="fileName">Name of the PQDIF file to be parsed.</param>
        public LogicalParser(string fileName)
        {
            m_physicalParser = new PhysicalParser(fileName);
            DataSourceRecords = new List<DataSourceRecord>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="LogicalParser"/> class.
        /// </summary>
        /// <param name="stream">The stream containing the PQDIF file data.</param>
        /// <param name="leaveOpen">True if the stream should be closed when the parser is closed; false otherwise.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="stream"/> is not both readable and seekable.</exception>
        public LogicalParser(Stream stream, bool leaveOpen = false)
        {
            m_physicalParser = new PhysicalParser();
            DataSourceRecords = new List<DataSourceRecord>();
            Open(stream, leaveOpen);
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the file name of the PQDIF file to be parsed.
        /// </summary>
        public string? FileName
        {
            get => m_physicalParser.FileName;
            set => m_physicalParser.FileName = value;
        }

        /// <summary>
        /// Gets the container record from the PQDIF file. This is
        /// parsed as soon as the parser is <see cref="Open()"/>ed.
        /// </summary>
        public ContainerRecord? ContainerRecord { get; private set; }

        /// <summary>
        /// Gets a list of all DataSource records from the PQDIF file. This is
        /// parsed when passing throug the observation records <see cref="NextObservationRecord()"/>ed.
        /// </summary>
        public List<DataSourceRecord> DataSourceRecords { get; private set; }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Opens the parser and parses the <see cref="ContainerRecord"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="FileName"/> has not been defined.</exception>
        /// <exception cref="InvalidDataException">First record of PQDIF file is not the container record.</exception>
        /// <exception cref="NotSupportedException">An unsupported compression mode was defined in the PQDIF file.</exception>
        public void Open()
        {
            m_physicalParser.Open();
            ContainerRecord = ContainerRecord.CreateContainerRecord(m_physicalParser.NextRecord());

            if (ContainerRecord == null)
                throw new InvalidDataException("The first record in a PQDIF file must be a container record.");

            m_physicalParser.CompressionAlgorithm = ContainerRecord.CompressionAlgorithm;
            m_physicalParser.CompressionStyle = ContainerRecord.CompressionStyle;
        }

        /// <summary>
        /// Opens the parser and parses the <see cref="ContainerRecord"/>.
        /// </summary>
        /// <param name="stream">The stream containing the PQDIF file data.</param>
        /// <param name="leaveOpen">True if the stream should be closed when the parser is closed; false otherwise.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="stream"/> is not both readable and seekable.</exception>
        /// <exception cref="InvalidDataException">First record of PQDIF file is not the container record.</exception>
        /// <exception cref="NotSupportedException">An unsupported compression mode was defined in the PQDIF file.</exception>
        public void Open(Stream stream, bool leaveOpen = false)
        {
            m_physicalParser.Open(stream, leaveOpen);
            ContainerRecord = ContainerRecord.CreateContainerRecord(m_physicalParser.NextRecord());

            if (ContainerRecord == null)
                throw new InvalidDataException("The first record in a PQDIF file must be a container record.");

            m_physicalParser.CompressionAlgorithm = ContainerRecord.CompressionAlgorithm;
            m_physicalParser.CompressionStyle = ContainerRecord.CompressionStyle;
        }

        /// <summary>
        /// Determines whether there are any more
        /// <see cref="ObservationRecord"/>s to be
        /// read from the PQDIF file.
        /// </summary>
        /// <returns>true if there is another observation record to be read from PQDIF file; false otherwise</returns>
        /// <exception cref="InvalidOperationException">PQDIF file has more than one container record.</exception>
        /// <exception cref="InvalidDataException">Observation record found with no corresponding data source record.</exception>
        public bool HasNextObservationRecord()
        {
            Record physicalRecord;
            RecordType recordType;

            // Read records from the file until we encounter an observation record or end of file
            while (m_nextObservationRecord == null && m_physicalParser.HasNextRecord())
            {
                physicalRecord = m_physicalParser.NextRecord();
                recordType = physicalRecord.Header.TypeOfRecord;

                switch (recordType)
                {
                    case RecordType.DataSource:
                        // Keep track of the latest data source record in order to associate it with observation records
                        m_currentDataSourceRecord = DataSourceRecord.CreateDataSourceRecord(physicalRecord)
                            ?? throw new InvalidDataException("Invalid assumption: record type is data source, yet the data source record was not created.");

                        DataSourceRecords.Add(m_currentDataSourceRecord);
                        break;

                    case RecordType.MonitorSettings:
                        // Keep track of the latest monitor settings record in order to associate it with observation records
                        m_currentMonitorSettingsRecord = MonitorSettingsRecord.CreateMonitorSettingsRecord(physicalRecord);
                        break;

                    case RecordType.Observation:
                        // Found an observation record!
                        if (m_currentDataSourceRecord == null)
                            throw new InvalidDataException("Found observation record before finding data source record.");

                        m_nextObservationRecord = ObservationRecord.CreateObservationRecord(physicalRecord, m_currentDataSourceRecord, m_currentMonitorSettingsRecord);
                        break;

                    case RecordType.Container:
                        // The container record is parsed when the file is opened; it should never be encountered here
                        throw new InvalidOperationException("Found more than one container record in PQDIF file.");

                    default:
                        // Ignore unrecognized record types as the rest of the file might be valid.
                        //throw new ArgumentOutOfRangeException(string.Format("Unknown record type: {0}", physicalRecord.Header.RecordTypeTag));
                        break;
                }
            }

            return m_nextObservationRecord != null;
        }

        /// <summary>
        /// Gets the next observation record from the PQDIF file.
        /// </summary>
        /// <returns>The next observation record.</returns>
        public ObservationRecord NextObservationRecord()
        {
            // Call this first to read ahead to the next
            // observation record if we haven't already
            HasNextObservationRecord();

            // We need to set m_nextObservationRecord to null so that
            // subsequent calls to HasNextObservationRecord() will
            // continue to parse new records
            ObservationRecord? nextObservationRecord = m_nextObservationRecord;
            m_nextObservationRecord = null;

            if (nextObservationRecord == null)
                throw new InvalidOperationException("There are no more observation records in the PQDIF file.");

            return nextObservationRecord;
        }

        /// <summary>
        /// Resets the parser to the beginning of the PQDIF file.
        /// </summary>
        public void Reset()
        {
            m_currentDataSourceRecord = null;
            m_currentMonitorSettingsRecord = null;
            m_nextObservationRecord = null;
            DataSourceRecords = new List<DataSourceRecord>();

            m_physicalParser.Reset();
            m_physicalParser.NextRecord(); // skip container record
        }

        /// <summary>
        /// Closes the PQDIF file.
        /// </summary>
        public void Close() =>
            m_physicalParser.Close();

        /// <summary>
        /// Releases resources held by the parser.
        /// </summary>
        public void Dispose() =>
            m_physicalParser.Dispose();

        #endregion
    }
}
