﻿// Copyright 2013-2014 Boban Jose
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.

using System;

namespace Graphene.Configuration
{
    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex);
    }

    internal class SysDiagLogger : ILogger
    {
        public void Debug(string message)
        {
            System.Diagnostics.Debug.Write(message);
        }

        public void Info(string message)
        {
            System.Diagnostics.Debug.Write(message);
        }

        public void Warn(string message)
        {
            System.Diagnostics.Debug.Write(message);
        }

        public void Error(string message, Exception ex)
        {
            System.Diagnostics.Debug.Write(message);
        }
    }
}