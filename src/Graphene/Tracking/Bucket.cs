﻿// Copyright 2013-2014 Boban Jose
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Graphene.Configuration;
using Graphene.Util;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;

namespace Graphene.Tracking
{
    internal class Bucket
    {
        private readonly ConcurrentDictionary<string, Counter> _counters;
        private readonly DateTime _expiresAfter;
        private readonly object _syncLock = new object();
        private readonly List<Bucket> _lowRezBuckets = new List<Bucket>();
        private readonly List<Resolution> _coveredResolutions = new List<Resolution>();
        private Resolution _resolution;

        internal Bucket(int lifeTimeInSeconds, Resolution minResolution, DateTime? timeNow = null, bool isLargerTimespanBucket = false)
        {
            DateTime timeNow1 = Configurator.Configuration.EvaluateDateTime();
            if (timeNow.HasValue)
            {
                timeNow1 = timeNow.Value;

                if (timeNow1.Kind == DateTimeKind.Unspecified)
                {
                    timeNow1 = DateTime.SpecifyKind(timeNow1, DateTimeKind.Utc);
                }
                    
            }
            _expiresAfter = timeNow1.AddSeconds(lifeTimeInSeconds);
            _counters = new ConcurrentDictionary<string, Counter>();
            _resolution = minResolution;
            TimeSlot = setTimeSlotDateTime(timeNow1, _resolution);
            if (isLargerTimespanBucket)
                _coveredResolutions.Add(minResolution);
            else
            {
                switch (minResolution)
                {
                    case Resolution.Minute:
                        _coveredResolutions.AddRange(new[] {Resolution.Minute});
                        break;
                    case Resolution.FiveMinute:
                        _coveredResolutions.AddRange(new[] {Resolution.FiveMinute, Resolution.Minute});
                        break;
                    case Resolution.FifteenMinute:
                        _coveredResolutions.AddRange(new[]
                        {Resolution.FifteenMinute, Resolution.FiveMinute, Resolution.Minute});
                        break;
                    case Resolution.ThirtyMinute:
                        _coveredResolutions.AddRange(new[]
                        {
                            Resolution.ThirtyMinute, Resolution.FifteenMinute, Resolution.FiveMinute, Resolution.Minute
                        });
                        break;
                    case Resolution.Hour:
                        _coveredResolutions.AddRange(new[]
                        {
                            Resolution.Hour, Resolution.ThirtyMinute, Resolution.FifteenMinute, Resolution.FiveMinute,
                            Resolution.Minute
                        });
                        break;
                    case Resolution.Day:
                        _coveredResolutions.AddRange(new[]
                        {
                            Resolution.Day, Resolution.Hour, Resolution.ThirtyMinute, Resolution.FifteenMinute,
                            Resolution.FiveMinute, Resolution.Minute
                        });
                        break;
                    case Resolution.Month:
                        _coveredResolutions.AddRange(new[]
                        {
                            Resolution.Month, Resolution.Day, Resolution.Hour, Resolution.ThirtyMinute,
                            Resolution.FifteenMinute, Resolution.FiveMinute, Resolution.Minute
                        });
                        break;
                }
                initalizeLowRezBuckets(lifeTimeInSeconds, minResolution);
            }
        }

        
        private void initalizeLowRezBuckets(int lifeTimeInSeconds, Resolution minResolution)
        {
            switch (minResolution)
            {
                case Resolution.FiveMinute:
                    _lowRezBuckets.AddRange(new Bucket[]
                    {
                        //new Bucket(lifeTimeInSeconds, Resolution.FifteenMinute, TimeSlot, true),
                        //new Bucket(lifeTimeInSeconds, Resolution.Hour, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Day, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Month, TimeSlot, true),
                    });
            
                    break;
                case Resolution.FifteenMinute:
                    _lowRezBuckets.AddRange(new Bucket[]
                    {
                        //new Bucket(lifeTimeInSeconds, Resolution.Hour, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Day, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Month, TimeSlot, true),
                    });
                    break;
                case Resolution.Hour:
                    _lowRezBuckets.AddRange(new Bucket[]
                    {
                        new Bucket(lifeTimeInSeconds, Resolution.Day, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Month, TimeSlot, true),
                    });
                    break;
                case Resolution.ThirtyMinute:
                    _lowRezBuckets.AddRange(new Bucket[]
                    {
                        //new Bucket(lifeTimeInSeconds, Resolution.Hour, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Day, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Month, TimeSlot, true),
                    });
                    break;
                case Resolution.Minute:
                    _lowRezBuckets.AddRange(new Bucket[]
                    {
                        //new Bucket(lifeTimeInSeconds, Resolution.FifteenMinute, TimeSlot, true),
                        //new Bucket(lifeTimeInSeconds, Resolution.Hour, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Day, TimeSlot, true),
                        new Bucket(lifeTimeInSeconds, Resolution.Month, TimeSlot, true),
                    });
                    break;
            }
        }

        public List<Bucket> LowResolutionBuckets
        {
            get { return _lowRezBuckets; }
        }

        public List<Resolution> CoveredResolutions
        {
            get { return _coveredResolutions; }
        } 

        internal ConcurrentDictionary<string, Counter> Counters
        {
            get { return _counters; }
        }

        internal DateTime TimeSlot { get; private set; }

        internal bool HasExpired
        {
            get { return Configurator.Configuration.EvaluateDateTime() > _expiresAfter; }
        }

        internal Resolution BucketResolution
        {
            get { return _resolution; }
        }

        internal void IncrementCounter(long by)
        {
            IncrementCounter(by, null, null);
        }

        internal void IncrementCounter(long by, string metricName)
        {
            IncrementCounter(by, metricName, null);
        }

        internal void IncrementCounter(long by, object filter)
        {
            IncrementCounter(by, null, filter);
        }

        internal void IncrementCounter(long by, string metricName, object filter)
        {
            string keyTag = string.Empty;
            List<String> propertyNv = null;

            if (filter != null)
            {
                propertyNv = filter.GetPropertyNameValueListSorted();
                if (propertyNv.Count > 0)
                    keyTag = propertyNv.Aggregate((x, z) => string.Concat(x, ",", z));
            }

            Counter counter = null;
            if (_counters.ContainsKey(keyTag))
                counter = _counters[keyTag];
            else
            {
                lock (_syncLock)
                {
                    if (_counters.ContainsKey(keyTag))
                        counter = _counters[keyTag];
                    else
                    {
                        counter = new Counter();
                        if (propertyNv != null && propertyNv.Count > 0)
                        {
                            var searchTags = new List<string>();
                            searchTags.Add(propertyNv.Aggregate((x, z) => string.Concat(x, ",,", z)));
                            getAllSearchTags(propertyNv, searchTags);
                            counter.SearchTags = searchTags;
                            counter.KeyFilter = keyTag;
                        }
                        _counters.TryAdd(keyTag, counter);
                    }
                }
            }
            counter.Increment(by, metricName);

            _lowRezBuckets.ForEach(b => b.IncrementCounter(by, metricName, filter));
        }

        internal void setTimeSlot(DateTime timeSlot)
        {
            TimeSlot = timeSlot;
        }

        public void getAllSearchTags(List<string> filters, List<string> perms)
        {
            var s = new List<string>();
            var nvPairsDouble = new List<string>();
            nvPairsDouble.AddRange(filters);
            nvPairsDouble.AddRange(filters);

            for (int i = 0; i < filters.Count; i++)
            {
                for (int k = 0; k < filters.Count; k++)
                {
                    var tag = new List<string>();
                    int skipped = 0;
                    int picked = 0;
                    for (int j = 0; j < nvPairsDouble.Count; j++)
                    {
                        if (skipped > i + k)
                        {
                            tag.Add(nvPairsDouble[j]);
                            picked++;
                        }
                        else
                            skipped++;
                        if (picked + skipped >= filters.Count + k)
                            break;
                    }
                    tag.Sort();
                    if (tag.Count > 0)
                    {
                        string newTag = tag.Aggregate((x, z) => string.Concat(x, ",,", z));
                        if (!perms.Contains(newTag))
                        {
                            perms.Add(newTag);
                            if (tag.Count >= 3)
                                getAllSearchTags(tag, perms);
                        }
                    }
                }
            }
        }

        private DateTime setTimeSlotDateTime(DateTime timeNow, Resolution adjustmentResolution)
        {
            DateTime dateTimeToReturn = timeNow;
            switch (adjustmentResolution)
            {
                case Resolution.Minute:
                    dateTimeToReturn = timeNow.Round(TimeSpan.FromMinutes(1));
                    break;
                case Resolution.FiveMinute:
                    dateTimeToReturn = timeNow.Round(TimeSpan.FromMinutes(5));
                    break;
                case Resolution.FifteenMinute:
                    dateTimeToReturn = timeNow.Round(TimeSpan.FromMinutes(15));
                    break;
                case Resolution.ThirtyMinute:
                    dateTimeToReturn = timeNow.Round(TimeSpan.FromMinutes(30));
                    break;
                case Resolution.Hour:
                    dateTimeToReturn = timeNow.Round(TimeSpan.FromHours(1));
                    break;
                case Resolution.Day:
                    dateTimeToReturn = DateTime.SpecifyKind((new DateTime(timeNow.Year, timeNow.Month, timeNow.Day) + Configurator.Configuration.DayTotalTZOffset),DateTimeKind.Utc);
                    switch (Configurator.Configuration.GrapheneRoundingMethod)
                    {
                        case TimespanRoundingMethod.Start:
                            if (dateTimeToReturn > timeNow)
                             {
                                 dateTimeToReturn = timeNow.AddDays(-1);
                             }
                            break;
                        case TimespanRoundingMethod.End:
                            if (dateTimeToReturn < timeNow)
                             {
                                 dateTimeToReturn = timeNow.AddDays(1);
                             }
                            break;
                        case TimespanRoundingMethod.MidPoint:
                            if (dateTimeToReturn > timeNow.AddMinutes(-30))
                             {
                                 dateTimeToReturn = timeNow.AddDays(-1);
                             }
                            break;
                    }
                    break;
                case Resolution.Month:
                    dateTimeToReturn = new DateTime(timeNow.Year, timeNow.Month, 1) +
                                            Configurator.Configuration.DayTotalTZOffset;
                    switch (Configurator.Configuration.GrapheneRoundingMethod)
                    {
                        case TimespanRoundingMethod.Start:
                            if (dateTimeToReturn > timeNow)
                             {
                                 dateTimeToReturn = timeNow.AddMonths(-1);
                             }
                            break;
                        case TimespanRoundingMethod.End:
                            if (dateTimeToReturn < timeNow)
                             {
                                 dateTimeToReturn = timeNow.AddMonths(1);
                             }
                            break;
                        case TimespanRoundingMethod.MidPoint:
                            if (dateTimeToReturn > timeNow.AddMinutes(-30))
                             {
                                 dateTimeToReturn = timeNow.AddMonths(-1);
                             }
                            break;
                    }
                    break;
            }

            return dateTimeToReturn;
        }
    }
}