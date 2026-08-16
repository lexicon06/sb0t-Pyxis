using Supabase;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text;
using System.Security.Cryptography;
using Supabase.Gotrue;

namespace core.Udp
{
    

    class SupabaseClient
    {

        private static Supabase.Client _client;
        private static Session _session;
        private static string _url = "";
        private static string _key = "";

        /// <summary>
        /// True when a Supabase URL and key have been configured, so the node
        /// list fallback can be used.
        /// </summary>
        public static bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(_url) && !string.IsNullOrEmpty(_key); }
        }

        public static async Task<bool> InitializeAsync(string url, string key)
        {
            try
            {
                _url = url ?? "";
                _key = key ?? "";

                if (!IsConfigured)
                    return false;

                if (_client == null)
                {
                    _client = new Supabase.Client(url, key);
                    ServerCore.Log("Supabase client inicializado");
                    await _client.InitializeAsync();
                }


                if (_session == null || _session?.User == null)
                {
                    ServerCore.Log("Inició sesión con Supabase.");
                    _session = await _client.Auth.SignIn("", "");
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<List<SupabaseChatroomModel>> GetChatroomsAsync()
        {
            if (_client == null)
                throw new InvalidOperationException("Supabase client no ha sido inicializado.");

            var response = await _client.From<SupabaseChatroomModel>().Get();
            return response.Models;
        }
    }
}
