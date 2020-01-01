// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al..
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Tests.Messages
{
	using System;
    using System.Collections.Generic;
    using System.Linq;

    [Serializable]
	public class SerializationTestMediumMessage :
		IEquatable<SerializationTestMediumMessage>
	{
        public ICollection<SerializationTestMessage> Items { get; set; } = new List<SerializationTestMessage>();
        public bool Equals(SerializationTestMediumMessage obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.Items.OrderBy(item => item.GuidValue).SequenceEqual(Items.OrderBy(item => item.GuidValue));
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof (SerializationTestMediumMessage)) return false;
			return Equals((SerializationTestMediumMessage) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
                int hashCode = this.Items.Count.GetHashCode();
                foreach (SerializationTestMessage item in this.Items)
                {
                    hashCode = (hashCode * 397) ^ item.GetHashCode();
                }
                return hashCode;
            }
        }

    
	}
}
