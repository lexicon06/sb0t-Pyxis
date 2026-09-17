/*
    sb0t ares chat server
    Copyright (C) 2017  AresChat

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Heijden.DNS;

namespace core
{
    /// <summary>
    /// Global ASN lookup cache. The DNS query is blocking, so we cache per IP
    /// to avoid stalling the server loop on every join / whois.
    /// </summary>
    static class AsnLookup
    {
        private static readonly Dictionary<String, uint> cache = new Dictionary<String, uint>();
        private static readonly object _lock = new object();
        private static readonly Resolver resolver = new Resolver();

        public static uint Get(IPAddress ip)
        {
            if (ip == null)
                return 0;

            String key = ip.ToString();

            lock (_lock)
            {
                uint cached;

                if (cache.TryGetValue(key, out cached))
                    return cached;
            }

            uint asn = 0;

            try
            {
                string reverseIp = new IPAddress(ip.GetAddressBytes().Reverse().ToArray()).ToString();
                Response response = resolver.Query(reverseIp + ".origin.asn.cymru.com", QType.TXT);
                string first = response.RecordsTXT.FirstOrDefault()?.TXT.FirstOrDefault();

                if (first != null)
                    asn = uint.Parse(first.Split('|').FirstOrDefault().Trim());
            }
            catch
            {
                asn = 0;
            }

            lock (_lock)
                cache[key] = asn;

            return asn;
        }
    }
}
