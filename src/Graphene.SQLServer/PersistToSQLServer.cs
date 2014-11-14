﻿// Copyright 2013-2014 Boban Jose
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.


using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Graphene.Configuration;
using Graphene.Data;
using Graphene.Publishing;
using Microsoft.SqlServer.Server;

namespace Graphene.SQLServer
{
    public class PersistToSQLServer : IPersist
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public PersistToSQLServer(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public void Persist(TrackerData trackerData)
        {
            try
            {
                persitTracker(trackerData);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
            }
        }

        private void persitTracker(TrackerData trackerData)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "dbo.UpdateTracker";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add("@TrackerID", SqlDbType.NVarChar);
                    command.Parameters["@TrackerID"].Value = String.Concat(trackerData.TypeName, "-", trackerData.KeyFilter).Replace(" ", "");

                    command.Parameters.Add("@Name", SqlDbType.NVarChar);
                    command.Parameters["@Name"].Value = trackerData.Name;

                    command.Parameters.Add("@TypeName", SqlDbType.NVarChar);
                    command.Parameters["@TypeName"].Value = trackerData.TypeName;

                    command.Parameters.Add("@KeyFilter", SqlDbType.NVarChar);
                    command.Parameters["@KeyFilter"].Value = trackerData.KeyFilter;

                    command.Parameters.Add("@TimeSlot", SqlDbType.DateTime);
                    command.Parameters["@TimeSlot"].Value = trackerData.TimeSlot.ToUniversalTime();

                    SqlParameter flParameter;
                    flParameter = command.Parameters.AddWithValue("@FilterList", createFilterDataTable(trackerData));
                    flParameter.SqlDbType = SqlDbType.Structured;
                    flParameter.TypeName = "dbo.FilterList";

                    SqlParameter mParameter;
                    mParameter = command.Parameters.AddWithValue("@Measurement", createMeasurementDataTable(trackerData));
                    mParameter.SqlDbType = SqlDbType.Structured;
                    mParameter.TypeName = "dbo.Measurement";

                    command.ExecuteNonQuery();
                }
            }
        }

        private DataTable createFilterDataTable(TrackerData trackerData)
        {
            var table = new DataTable();
            table.Columns.Add("Filter", typeof(string));
            foreach (string filter in trackerData.SearchFilters)
            {
                table.Rows.Add(filter);
            }
            return table;
        }

        private static DataTable createMeasurementDataTable(TrackerData trackerData)
        {
            var table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Value", typeof(long));
            foreach (var metrics in trackerData.Measurement.NamedMetrics)
            {
                table.Rows.Add(metrics.Key, metrics.Value);
            }
            table.Rows.Add("_Occurrence", trackerData.Measurement._Occurrence);
            table.Rows.Add("_Total", trackerData.Measurement._Total);
            return table;
        }
    }
}