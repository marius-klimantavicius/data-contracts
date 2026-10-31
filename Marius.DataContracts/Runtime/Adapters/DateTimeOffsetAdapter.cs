// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Globalization;


namespace Marius.DataContracts.Runtime.Adapters
{
    [DataContract(Name = "DateTimeOffset", Namespace = "http://schemas.datacontract.org/2004/07/System")]
    public struct DateTimeOffsetAdapter
    {
        [DataMember(Name = "DateTime", IsRequired = true)]
        public DateTime UtcDateTime { get; set; }

        [DataMember(Name = "OffsetMinutes", IsRequired = true)]
        public short OffsetMinutes { get; set; }

        public DateTimeOffsetAdapter(DateTime dateTime, short offsetMinutes)
        {
            UtcDateTime = dateTime;
            OffsetMinutes = offsetMinutes;
        }

        public static DateTimeOffset GetOriginal(DateTimeOffsetAdapter value)
        {
            try
            {
                switch (value.UtcDateTime.Kind)
                {
                    case DateTimeKind.Unspecified:
                        return new DateTimeOffset(value.UtcDateTime, new TimeSpan(0, value.OffsetMinutes, 0));

                    //DateTimeKind.Utc and DateTimeKind.Local
                    //Read in deserialized DateTime portion of the DateTimeOffsetAdapter and convert DateTimeKind to Unspecified.
                    //Apply offset information read from OffsetMinutes portion of the DateTimeOffsetAdapter.
                    //Return converted DateTimeOffset object.
                    default:
                        var deserialized = new DateTimeOffset(value.UtcDateTime);
                        return deserialized.ToOffset(new TimeSpan(0, value.OffsetMinutes, 0));
                }
            }
            catch (ArgumentException exception)
            {
                throw XmlExceptionHelper.CreateConversionException(value.ToString(CultureInfo.InvariantCulture), "DateTimeOffset", exception);
            }
        }

        public static DateTimeOffsetAdapter GetAdapter(DateTimeOffset value)
        {
            return new DateTimeOffsetAdapter(value.UtcDateTime, (short)value.Offset.TotalMinutes);
        }

#pragma warning disable IDE0060 // https://github.com/dotnet/runtime/issues/76012
        public string ToString(IFormatProvider _)
        {
            return "DateTime: " + UtcDateTime + ", Offset: " + OffsetMinutes;
        }
#pragma warning restore IDE0060
    }
}