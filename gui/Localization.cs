using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace gui
{
    /// <summary>
    /// Multi-language support. Tab navigation stays in English; content labels
    /// are translated. Layout is never touched by language switching.
    /// </summary>
    public static class GUILabels
    {
        public static Dictionary<String, String> english = new Dictionary<String, String>();
        public static Dictionary<String, String> spanish = new Dictionary<String, String>();

        public static bool IsSpanish { get; set; }

        public static void Setup(MainWindow w)
        {
            english["button1a"] = "Start server";
            spanish["button1a"] = "Comenzar el servidor";

            english["button1b"] = "Stop server";
            spanish["button1b"] = "Detener el servidor";

            english["statuslabela"] = "Status: Server stopped.";
            spanish["statuslabela"] = "Estado: Servidor detenido.";

            english["statuslabelb"] = "Status: Server running.";
            spanish["statuslabelb"] = "Estado: Servidor funcionando";

            english["mboxa"] = "Unable to start server - please check your settings";
            spanish["mboxa"] = "No se puede iniciar el servidor - Por favor revise su configuración";

            english["mboxb"] = "Are you sure you want to quit sb0t?";
            spanish["mboxb"] = "Estás seguro que deseas salir de Sb0t?";

            english["mboxc"] = "Invalid leaf identifier";
            spanish["mboxc"] = "Identificador de hoja invalido";

            english["mboxd"] = "This leaf already exists in your trusted leaf list";
            spanish["mboxd"] = "Está hoja ya existe en su lista de hojas de confianza";

            english["mboxe"] = "Invalid sb0t extension";
            spanish["mboxe"] = "Extensión de sb0t invalida";

            english["label2"] = "Room name:";
            spanish["label2"] = "Nombre de la sala:";

            english["label3"] = "Room port:";
            spanish["label3"] = "Puerto de la sala:";

            english["label4"] = "Bot name:";
            spanish["label4"] = "Nombre del bot:";

            english["checkBox1"] = "Chat logging enabled";
            spanish["checkBox1"] = "Registro de Chat habilitado";

            english["checkBox2"] = "Room scribbles enabled";
            spanish["checkBox2"] = "Scribbles en sala habilitados";

            english["checkBox3"] = "Start server when sb0t starts";
            spanish["checkBox3"] = "Iniciar servidor cuando sb0t inicie";

            english["checkBox4"] = "Start sb0t minimized";
            spanish["checkBox4"] = "Iniciar sb0t minimizado";

            english["checkBox5"] = "Show room on channel list";
            spanish["checkBox5"] = "Mostrar la sala en la lista de canales";

            english["checkBox6"] = "Support voice chat";
            spanish["checkBox6"] = "Soporte de chat de voz";

            english["checkBox9"] = "ib0t support enabled";
            spanish["checkBox9"] = "Soporte de ib0t habilitado";

            english["button2"] = "Open data folder";
            spanish["button2"] = "Abrir la carpeta de datos";

            english["button3"] = "Ares join";
            spanish["button3"] = "Unirse a Ares";

            english["button4"] = "cb0t join";
            spanish["button4"] = "Unirse a cb0t";

            english["label5"] = "Owner password:";
            spanish["label5"] = "Contraseña del propietario:";

            english["checkBox10"] = "Enable built in commands";
            spanish["checkBox10"] = "Habilitar comandos integrados";

            english["checkBox22"] = "Check passwords against clients (strict mode)";
            spanish["checkBox22"] = "Revisar contraseñas a través de Clientes (Modo estricto)";

            english["label26"] = "Trusted leaves (hub mode)";
            spanish["label26"] = "Hojas de confianza (Modo centro)";

            english["label27"] = "Trusted leaf identifier:";
            spanish["label27"] = "Identificador hoja de confianza:";

            english["button7"] = "Add";
            spanish["button7"] = "Añadir";

            english["label25"] = "Your leaf identifier:";
            spanish["label25"] = "Su identificador de hoja:";

            english["label24"] = "Link mode:";
            spanish["label24"] = "Modo link:";

            english["checkBox20"] = "Auto reconnect";
            spanish["checkBox20"] = "Auto reconectar";

            english["checkBox21"] = "Allow linked admin";
            spanish["checkBox21"] = "Permitir linkeado por administrador";

            english["label22"] = "Available:";
            spanish["label22"] = "Disponible:";

            english["label23"] = "Refresh...";
            spanish["label23"] = "Actualizar...";

            english["label20"] = "Server avatar:";
            spanish["label20"] = "Avatar del servidor:";

            english["label21"] = "Default avatar:";
            spanish["label21"] = "Avatar por defecto";

            english["button5"] = "Update";
            spanish["button5"] = "Actualizar";

            english["button6"] = "Update";
            spanish["button6"] = "Actualizar";

            english["label9"] = "Advanced settings";
            spanish["label9"] = "Configuración avanzada";

            english["checkBox7"] = "Enable file browsing";
            spanish["checkBox7"] = "Habilitar navegación de archivos";

            english["checkBox18"] = "Hide IP addresses";
            spanish["checkBox18"] = "Esconder la dirección IP";

            english["checkBox23"] = "Enable room search";
            spanish["checkBox23"] = "Habilitar la búsqueda de salas";

            english["checkBox19"] = "Local clients auto login";
            spanish["checkBox19"] = "Inicio de sesión automática para clientes locales";

            english["label16"] = "Live Scripts Endpoint:";
            spanish["label16"] = "Punto final de scripts en vivo:";

            english["label17"] = "UDP host address:";
            spanish["label17"] = "Dirección host UDP:";

            english["label18"] = "Preferred language:";
            spanish["label18"] = "Idioma preferido:";

            english["label12"] = "Restrictions";
            spanish["label12"] = "Restricciones";

            english["label14"] = "Minimum age:";
            spanish["label14"] = "Edad mínima:";

            english["checkBox17"] = "Enable age restriction";
            spanish["checkBox17"] = "Habilitar restricción de edad";

            english["label15"] = "Gender";
            spanish["label15"] = "Género";

            english["checkBox15"] = "Reject male";
            spanish["checkBox15"] = "Rechazar hombres";

            english["checkBox14"] = "Reject female";
            spanish["checkBox14"] = "Rechazar mujeres";

            english["checkBox16"] = "Reject unknown";
            spanish["checkBox16"] = "Rechazar desconocidos";

            english["label10"] = "Ban lists";
            spanish["label10"] = "Listas de bans";

            english["label11"] = "Interval:";
            spanish["label11"] = "Intervalo:";

            english["label19"] = "hours";
            spanish["label19"] = "horas";

            english["checkBox13"] = "Auto clear bans";
            spanish["checkBox13"] = "Auto limpieza de bans";

            english["label8"] = "Captcha";
            spanish["label8"] = "Captcha";

            english["label13"] = "Mode:";
            spanish["label13"] = "Modo:";

            english["checkBox8"] = "Captcha enabled";
            spanish["checkBox8"] = "Captcha habilitado";

            english["label7"] = "Scripting";
            spanish["label7"] = "Scripting";

            english["checkBox11"] = "Enable javascript engine";
            spanish["checkBox11"] = "Habilitar motor javascript";

            english["checkBox12"] = "Enable in-room scripting";
            spanish["checkBox12"] = "Habilitar scripting en-sala";

            english["label6"] = "Scripting level:";
            spanish["label6"] = "Nivel de scripting:";

            english["checkBox30"] = "Scripts can change level";
            spanish["checkBox30"] = "Los scripts pueden cambiar de nivel";

            english["checkBox25"] = "Fonts enabled";
            spanish["checkBox25"] = "Fuentes habilitadas";

            IsSpanish = core.Settings.Get<bool>("is_spanish");

            if (IsSpanish)
                SetSpanish(w);
            else
                SetEnglish(w);
        }

        public static void SetSpanish(MainWindow w)
        {
            core.Settings.Set("is_spanish", true);
            IsSpanish = true;

            w.button1.Content = spanish["button1a"];
            w.button2.Content = spanish["button2"];
            w.button3.Content = spanish["button3"];
            w.button4.Content = spanish["button4"];
            w.button5.Content = spanish["button5"];
            w.button6.Content = spanish["button6"];
            w.button7.Content = spanish["button7"];

            w.label2.Text = spanish["label2"];
            w.label3.Text = spanish["label3"];
            w.label4.Text = spanish["label4"];
            w.label5.Text = spanish["label5"];
            w.label6.Text = spanish["label6"];
            w.label7.Text = spanish["label7"];
            w.label8.Text = spanish["label8"];
            w.label9.Text = spanish["label9"];
            w.label10.Text = spanish["label10"];
            w.label11.Text = spanish["label11"];
            w.label12.Text = spanish["label12"];
            w.label13.Text = spanish["label13"];
            w.label14.Text = spanish["label14"];
            w.label15.Text = spanish["label15"];
            w.label16.Text = spanish["label16"];
            w.label17.Text = spanish["label17"];
            w.label18.Text = spanish["label18"];
            w.label19.Text = spanish["label19"];
            w.label20.Text = spanish["label20"];
            w.label21.Text = spanish["label21"];
            w.label22.Text = spanish["label22"];
            w.label23.Text = spanish["label23"];
            w.label24.Text = spanish["label24"];
            w.label25.Text = spanish["label25"];
            w.label26.Text = spanish["label26"];
            w.label27.Text = spanish["label27"];

            w.checkBox1.Content = spanish["checkBox1"];
            w.checkBox2.Content = spanish["checkBox2"];
            w.checkBox3.Content = spanish["checkBox3"];
            w.checkBox4.Content = spanish["checkBox4"];
            w.checkBox5.Content = spanish["checkBox5"];
            w.checkBox6.Content = spanish["checkBox6"];
            w.checkBox7.Content = spanish["checkBox7"];
            w.checkBox8.Content = spanish["checkBox8"];
            w.checkBox9.Content = spanish["checkBox9"];
            w.checkBox10.Content = spanish["checkBox10"];
            w.checkBox11.Content = spanish["checkBox11"];
            w.checkBox12.Content = spanish["checkBox12"];
            w.checkBox13.Content = spanish["checkBox13"];
            w.checkBox14.Content = spanish["checkBox14"];
            w.checkBox15.Content = spanish["checkBox15"];
            w.checkBox16.Content = spanish["checkBox16"];
            w.checkBox17.Content = spanish["checkBox17"];
            w.checkBox18.Content = spanish["checkBox18"];
            w.checkBox19.Content = spanish["checkBox19"];
            w.checkBox20.Content = spanish["checkBox20"];
            w.checkBox21.Content = spanish["checkBox21"];
            w.checkBox22.Content = spanish["checkBox22"];
            w.checkBox23.Content = spanish["checkBox23"];
            w.checkBox25.Content = spanish["checkBox25"];
            w.checkBox30.Content = spanish["checkBox30"];
        }

        public static void SetEnglish(MainWindow w)
        {
            core.Settings.Set("is_spanish", false);
            IsSpanish = false;

            w.button1.Content = english["button1a"];
            w.button2.Content = english["button2"];
            w.button3.Content = english["button3"];
            w.button4.Content = english["button4"];
            w.button5.Content = english["button5"];
            w.button6.Content = english["button6"];
            w.button7.Content = english["button7"];

            w.label2.Text = english["label2"];
            w.label3.Text = english["label3"];
            w.label4.Text = english["label4"];
            w.label5.Text = english["label5"];
            w.label6.Text = english["label6"];
            w.label7.Text = english["label7"];
            w.label8.Text = english["label8"];
            w.label9.Text = english["label9"];
            w.label10.Text = english["label10"];
            w.label11.Text = english["label11"];
            w.label12.Text = english["label12"];
            w.label13.Text = english["label13"];
            w.label14.Text = english["label14"];
            w.label15.Text = english["label15"];
            w.label16.Text = english["label16"];
            w.label17.Text = english["label17"];
            w.label18.Text = english["label18"];
            w.label19.Text = english["label19"];
            w.label20.Text = english["label20"];
            w.label21.Text = english["label21"];
            w.label22.Text = english["label22"];
            w.label23.Text = english["label23"];
            w.label24.Text = english["label24"];
            w.label25.Text = english["label25"];
            w.label26.Text = english["label26"];
            w.label27.Text = english["label27"];

            w.checkBox1.Content = english["checkBox1"];
            w.checkBox2.Content = english["checkBox2"];
            w.checkBox3.Content = english["checkBox3"];
            w.checkBox4.Content = english["checkBox4"];
            w.checkBox5.Content = english["checkBox5"];
            w.checkBox6.Content = english["checkBox6"];
            w.checkBox7.Content = english["checkBox7"];
            w.checkBox8.Content = english["checkBox8"];
            w.checkBox9.Content = english["checkBox9"];
            w.checkBox10.Content = english["checkBox10"];
            w.checkBox11.Content = english["checkBox11"];
            w.checkBox12.Content = english["checkBox12"];
            w.checkBox13.Content = english["checkBox13"];
            w.checkBox14.Content = english["checkBox14"];
            w.checkBox15.Content = english["checkBox15"];
            w.checkBox16.Content = english["checkBox16"];
            w.checkBox17.Content = english["checkBox17"];
            w.checkBox18.Content = english["checkBox18"];
            w.checkBox19.Content = english["checkBox19"];
            w.checkBox20.Content = english["checkBox20"];
            w.checkBox21.Content = english["checkBox21"];
            w.checkBox22.Content = english["checkBox22"];
            w.checkBox23.Content = english["checkBox23"];
            w.checkBox25.Content = english["checkBox25"];
            w.checkBox30.Content = english["checkBox30"];
        }
    }
}
