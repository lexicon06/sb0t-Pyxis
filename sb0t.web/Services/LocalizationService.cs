using System;
using System.Collections.Generic;

namespace sb0t.web.Services
{
    /// <summary>
    /// Simple web panel localization (English / Spanish).
    /// Language preference is kept in the circuit (scoped service) so it can be
    /// switched live without a redirect; it is also persisted to the browser's
    /// localStorage via a small JS helper so the choice survives page reloads.
    /// </summary>
    public class LocalizationService
    {
        private bool _isSpanish;

        public LocalizationService()
        {
        }

        public bool IsSpanish
        {
            get { return _isSpanish; }
        }

        public void SetLanguage(bool spanish)
        {
            _isSpanish = spanish;
        }

        public void ApplySaved(string lang)
        {
            _isSpanish = lang == "es";
        }

        public string T(string key)
        {
            var dict = IsSpanish ? _es : _en;
            return dict.TryGetValue(key, out var v) ? v : key;
        }

        private static readonly Dictionary<string, string> _en = new()
        {
            // nav
            ["Status"] = "Status",
            ["Users"] = "Users",
            ["Bans"] = "Bans",
            ["Admin"] = "Admin",
            ["Advanced"] = "Advanced",
            ["Linking"] = "Linking",
            ["Proxy"] = "Proxy",
            ["Avatars"] = "Avatars",
            ["Settings"] = "Settings",
            ["Logs"] = "Logs",
            ["Logout"] = "Logout",

            // login
            ["LoginTitle"] = "sb0t Pyxis admin",
            ["LoginHint"] = "Enter your web panel password. It is shown in the console on first run and can be changed in Settings.",
            ["WebPanelPassword"] = "Web panel password",
            ["Login"] = "Login",
            ["LoginError"] = "Incorrect password.",

            // status
            ["ServerStatus"] = "Server status",
            ["ServerStatusSub"] = "Overall state of the chat server.",
            ["Running"] = "Running",
            ["Stopped"] = "Stopped",
            ["StartServer"] = "Start server",
            ["StopServer"] = "Stop server",
            ["ConnectedUsers"] = "Connected users",
            ["ConnectedUsersSub"] = "Users currently connected to the room.",
            ["Name"] = "Name",
            ["Level"] = "Level",
            ["IP"] = "IP",
            ["Kick"] = "Kick",
            ["Ban"] = "Ban",
            ["NoUsers"] = "No users connected.",
            ["BannedUsers"] = "Banned Users",
            ["BannedUsersSub"] = "Users currently banned from the room.",
            ["Version"] = "Version",
            ["Guid"] = "GUID",
            ["Unban"] = "Unban",
            ["NoBans"] = "No users are banned.",

            // commands
            ["AdminCommands"] = "Admin commands",
            ["AdminCommandsSub"] = "Owner password, command toggles and per-command permission levels.",
            ["OwnerPassword"] = "Owner password",
            ["OwnerPasswordRoom"] = "Owner password (room)",
            ["EnableCommands"] = "Enable built in commands",
            ["StrictMode"] = "Strict mode",
            ["Save"] = "Save",
            ["CommandLevels"] = "Command levels",
            ["CommandLevelsSub"] = "Set which admin level can use each command.",
            ["FilterCommands"] = "Filter commands...",
            ["Command"] = "Command",

            // advanced
            ["AdvancedSettings"] = "Advanced settings",
            ["BanLists"] = "Ban lists",
            ["AutoClearInterval"] = "Auto clear interval (hours)",
            ["AutoClearBans"] = "Auto clear bans",
            ["Restrictions"] = "Restrictions",
            ["EnableAgeRestriction"] = "Enable age restriction",
            ["MinimumAge"] = "Minimum age",
            ["RejectMale"] = "Reject male",
            ["RejectFemale"] = "Reject female",
            ["RejectUnknown"] = "Reject unknown",
            ["Captcha"] = "Captcha",
            ["CaptchaEnabled"] = "Captcha enabled",
            ["Mode"] = "Mode",
            ["Regular"] = "Regular",
            ["Quarantine"] = "Quarantine",
            ["Scripting"] = "Scripting",
            ["EnableJsEngine"] = "Enable javascript engine",
            ["EnableInRoomScripting"] = "Enable in-room scripting",
            ["ScriptingLevel"] = "Scripting level",
            ["ScriptsCanChangeLevel"] = "Scripts can change level",
            ["ServerOptions"] = "Server options",
            ["EnableFileBrowsing"] = "Enable file browsing",
            ["EnableRoomSearch"] = "Enable room search",
            ["HideIPs"] = "Hide IP addresses",
            ["LocalAutoLogin"] = "Local clients auto login",
            ["Network"] = "Network",
            ["LseEndpoint"] = "Live Scripts Endpoint",
            ["UdpAddress"] = "UDP host address",
            ["PreferredLanguage"] = "Preferred language",
            ["SaveAll"] = "Save all",
            ["Saved"] = "Settings saved.",

            // linking
            ["TrustedLeaves"] = "Trusted leaves (hub mode)",
            ["AddTrustedLeaf"] = "Add trusted leaf",
            ["LeafIdentifierInput"] = "Identifier (sblnk://...)",
            ["Add"] = "Add",
            ["Remove"] = "Remove",
            ["NoLeaves"] = "No trusted leaves.",
            ["LinkingTitle"] = "Linking",
            ["LinkMode"] = "Link mode",
            ["Disabled"] = "Disabled",
            ["Hub"] = "Hub",
            ["Leaf"] = "Leaf",
            ["YourLeafIdentifier"] = "Your leaf identifier",
            ["AutoReconnect"] = "Auto reconnect",
            ["AllowLinkedAdmin"] = "Allow linked admin",
            ["LeafExists"] = "This leaf already exists.",
            ["InvalidLeaf"] = "Invalid leaf identifier.",

            // proxy
            ["TrustedProxies"] = "Trusted proxy IPs",
            ["ProxyHint"] = "localhost is always trusted. The server reads X-Forwarded-For / X-Real-IP from these proxies.",
            ["ProxyIp"] = "Proxy IP address",
            ["NoProxies"] = "No trusted proxies.",
            ["InvalidIp"] = "Invalid IP address.",

            // avatars
            ["ServerAvatar"] = "Server avatar",
            ["ServerAvatarSub"] = "Shown as the bot's avatar. Resized to 90x90.",
            ["DefaultAvatar"] = "Default avatar",
            ["DefaultAvatarSub"] = "Shown for users who don't have an avatar. Resized to 90x90.",
            ["ServerAvatarUpdated"] = "Server avatar updated.",
            ["DefaultAvatarUpdated"] = "Default avatar updated.",
            ["AvatarFailed"] = "Failed to update avatar.",

            // settings
            ["SettingsTitle"] = "Settings",
            ["SettingsSub"] = "Basic server and panel configuration.",
            ["RoomName"] = "Room name",
            ["RoomPort"] = "Room port",
            ["BotName"] = "Bot name",
            ["WebPasswordField"] = "Web panel password",

            // logs
            ["ServerLog"] = "Server log",
            ["Refresh"] = "Refresh",
        };

        private static readonly Dictionary<string, string> _es = new()
        {
            ["Status"] = "Estado",
            ["Users"] = "Usuarios",
            ["Admin"] = "Admin",
            ["Advanced"] = "Avanzado",
            ["Linking"] = "Enlace",
            ["Proxy"] = "Proxy",
            ["Avatars"] = "Avatares",
            ["Settings"] = "Ajustes",
            ["Logs"] = "Registros",
            ["Logout"] = "Salir",

            ["LoginTitle"] = "sb0t Pyxis admin",
            ["LoginHint"] = "Ingresa tu contraseña del panel web. Se muestra en la consola la primera vez y puede cambiarse en Ajustes.",
            ["WebPanelPassword"] = "Contraseña del panel web",
            ["Login"] = "Ingresar",
            ["LoginError"] = "Contraseña incorrecta.",

            ["ServerStatus"] = "Estado del servidor",
            ["ServerStatusSub"] = "Estado general del servidor de chat.",
            ["Running"] = "En ejecución",
            ["Stopped"] = "Detenido",
            ["StartServer"] = "Iniciar servidor",
            ["StopServer"] = "Detener servidor",
            ["ConnectedUsers"] = "Usuarios conectados",
            ["ConnectedUsersSub"] = "Usuarios actualmente conectados a la sala.",
            ["Name"] = "Nombre",
            ["Level"] = "Nivel",
            ["IP"] = "IP",
            ["Kick"] = "Expulsar",
            ["Ban"] = "Banear",
            ["NoUsers"] = "No hay usuarios conectados.",
            ["BannedUsers"] = "Usuarios baneados",
            ["BannedUsersSub"] = "Usuarios actualmente baneados de la sala.",
            ["Version"] = "Versión",
            ["Guid"] = "GUID",
            ["Unban"] = "Desbanear",
            ["NoBans"] = "No hay usuarios baneados.",

            ["AdminCommands"] = "Comandos de administrador",
            ["AdminCommandsSub"] = "Contraseña del propietario, interruptores de comandos y niveles de permiso por comando.",
            ["OwnerPassword"] = "Contraseña del propietario",
            ["OwnerPasswordRoom"] = "Contraseña del propietario (sala)",
            ["EnableCommands"] = "Habilitar comandos integrados",
            ["StrictMode"] = "Modo estricto",
            ["Save"] = "Guardar",
            ["CommandLevels"] = "Niveles de comando",
            ["CommandLevelsSub"] = "Define qué nivel de administrador puede usar cada comando.",
            ["FilterCommands"] = "Filtrar comandos...",
            ["Command"] = "Comando",

            ["AdvancedSettings"] = "Configuración avanzada",
            ["BanLists"] = "Listas de bans",
            ["AutoClearInterval"] = "Intervalo de limpieza (horas)",
            ["AutoClearBans"] = "Auto limpiar bans",
            ["Restrictions"] = "Restricciones",
            ["EnableAgeRestriction"] = "Habilitar restricción de edad",
            ["MinimumAge"] = "Edad mínima",
            ["RejectMale"] = "Rechazar hombres",
            ["RejectFemale"] = "Rechazar mujeres",
            ["RejectUnknown"] = "Rechazar desconocidos",
            ["Captcha"] = "Captcha",
            ["CaptchaEnabled"] = "Captcha habilitado",
            ["Mode"] = "Modo",
            ["Regular"] = "Normal",
            ["Quarantine"] = "Cuarentena",
            ["Scripting"] = "Scripting",
            ["EnableJsEngine"] = "Habilitar motor javascript",
            ["EnableInRoomScripting"] = "Habilitar scripting en-sala",
            ["ScriptingLevel"] = "Nivel de scripting",
            ["ScriptsCanChangeLevel"] = "Los scripts pueden cambiar de nivel",
            ["ServerOptions"] = "Opciones del servidor",
            ["EnableFileBrowsing"] = "Habilitar navegación de archivos",
            ["EnableRoomSearch"] = "Habilitar búsqueda de salas",
            ["HideIPs"] = "Ocultar direcciones IP",
            ["LocalAutoLogin"] = "Inicio de sesión automático local",
            ["Network"] = "Red",
            ["LseEndpoint"] = "Punto final de scripts en vivo",
            ["UdpAddress"] = "Dirección host UDP",
            ["PreferredLanguage"] = "Idioma preferido",
            ["SaveAll"] = "Guardar todo",
            ["Saved"] = "Ajustes guardados.",

            ["TrustedLeaves"] = "Hojas de confianza (modo centro)",
            ["AddTrustedLeaf"] = "Agregar hoja de confianza",
            ["LeafIdentifierInput"] = "Identificador (sblnk://...)",
            ["Add"] = "Agregar",
            ["Remove"] = "Quitar",
            ["NoLeaves"] = "No hay hojas de confianza.",
            ["LinkingTitle"] = "Enlace",
            ["LinkMode"] = "Modo de enlace",
            ["Disabled"] = "Desactivado",
            ["Hub"] = "Centro",
            ["Leaf"] = "Hoja",
            ["YourLeafIdentifier"] = "Tu identificador de hoja",
            ["AutoReconnect"] = "Auto reconectar",
            ["AllowLinkedAdmin"] = "Permitir admin enlazado",
            ["LeafExists"] = "Esta hoja ya existe.",
            ["InvalidLeaf"] = "Identificador de hoja inválido.",

            ["TrustedProxies"] = "IPs de proxy de confianza",
            ["ProxyHint"] = "localhost siempre es de confianza. El servidor lee X-Forwarded-For / X-Real-IP de estos proxies.",
            ["ProxyIp"] = "Dirección IP del proxy",
            ["NoProxies"] = "No hay proxies de confianza.",
            ["InvalidIp"] = "Dirección IP inválida.",

            ["ServerAvatar"] = "Avatar del servidor",
            ["ServerAvatarSub"] = "Se muestra como avatar del bot. Redimensionado a 90x90.",
            ["DefaultAvatar"] = "Avatar por defecto",
            ["DefaultAvatarSub"] = "Se muestra a los usuarios sin avatar. Redimensionado a 90x90.",
            ["ServerAvatarUpdated"] = "Avatar del servidor actualizado.",
            ["DefaultAvatarUpdated"] = "Avatar por defecto actualizado.",
            ["AvatarFailed"] = "No se pudo actualizar el avatar.",

            ["SettingsTitle"] = "Ajustes",
            ["SettingsSub"] = "Configuración básica del servidor y del panel.",
            ["RoomName"] = "Nombre de la sala",
            ["RoomPort"] = "Puerto de la sala",
            ["BotName"] = "Nombre del bot",
            ["WebPasswordField"] = "Contraseña del panel web",

            ["ServerLog"] = "Registro del servidor",
            ["Refresh"] = "Actualizar",
        };
    }
}
