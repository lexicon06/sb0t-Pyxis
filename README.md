# 🚀 sb0t Pyxis

**sb0t Pyxis** es un **servidor de chat Ares** moderno y completo. Te permite crear tu propia sala de chat, conectarte con clientes Ares (como inbizio) y administrarla desde un panel web, todo desde tu propia computadora o servidor.

Este proyecto es la versión moderna y renovada de sb0t, el clásico servidor de chat Ares:

- ✅ **.NET 8** — tecnología moderna y rápida
- ✅ **Interfaz gráfica de escritorio** (Avalonia) — funciona en Windows, Linux y macOS
- ✅ **Panel web de administración** (Blazor Server) — administrá la sala desde el navegador
- ✅ **Multiplataforma** — los datos se guardan en un archivo JSON (no más registro de Windows)
- ✅ **En español e inglés** — elegí el idioma con un clic

---

## ✨ ¿Qué puedo hacer con sb0t Pyxis?

- **Crear una sala de chat** a la que se conectan los clientes Ares (inbizio, etc.)
- **Ver quién está conectado** en tiempo real y desde dónde
- **Expulsar (kick)** y **banear** usuarios — y desbanearlos cuando quieras
- **Subir avatares** (del servidor y el avatar por defecto)
- **Configurar todo**: nombre de la sala, puerto, bot, contraseña del dueño, niveles de comandos, captcha, proxies, enlaces entre servidores (hub/leaf) y mucho más
- **Ver los registros (logs)** del servidor en vivo
- **Conectar salas entre sí** (modo Hub / Leaf) para armar una red de servidores

---

## 📦 Descargas (Releases)

Cuando publiquemos una versión nueva, los archivos listos para usar estarán en la página de **Releases**:

👉 **https://github.com/lexicon06/sb0t-Pyxis/releases**

Cada release traerá **cuatro archivos**:

| Archivo | Qué es | ¿Necesita instalar algo? |
|---|---|---|
| `sb0t-Pyxis-gui-win-x64-SELF-CONTAINED.zip` ⭐ | Aplicación de escritorio para **Windows 64 bits** | ❌ No — funciona directo |
| `sb0t-Pyxis-web-win-x64-SELF-CONTAINED.zip` ⭐ | **Panel web** para **Windows 64 bits** | ❌ No — funciona directo |
| `sb0t-Pyxis-gui-win-x64.zip` | Aplicación de escritorio (más liviana) | ✅ Requiere **.NET 8 Desktop Runtime** |
| `sb0t-Pyxis-web-win-x64.zip` | Panel web (más liviano) | ✅ Requiere **.NET 8 Desktop Runtime** |

> ⭐ **Recomendado:** las versiones **SELF-CONTAINED** traen todo incluido — el usuario solo descomprime y ejecuta, sin instalar nada. Elegí estas si querés la experiencia más simple.
>
> **¿No sabés si tu PC es de 64 bits?** Casi todas las computadoras modernas lo son. En Windows abrí el *Explorador de archivos* → clic derecho en *Este equipo* → *Propiedades* y mirá "Tipo de sistema".

---

## 🖥️ Cómo usarlo (guía paso a paso para principiantes)

### Opción A: Solo la aplicación de escritorio (la más fácil)

1. Descargá **`sb0t-Pyxis-gui-win-x64-SELF-CONTAINED.zip`** desde Releases (el que trae todo incluido).
2. Hacé clic derecho sobre el `.zip` y elegí **"Extraer todo..."**.
3. Entrá a la carpeta extraída y doble clic en **`sb0t.exe`**.
4. En la ventana que se abre, completá los datos básicos:
   - **Nombre**: el nombre de tu sala (ej: "Sala de Juan")
   - **Puerto**: dejalo en `54321` si no sabés qué poner
   - **Bot**: el nombre del bot
5. Clic en **Iniciar servidor** (el botón grande arriba).
6. ✅ ¡Listo! Tu sala está corriendo. Los clientes Ares pueden conectarse a tu IP en ese puerto.

> 💡 **¿Usás la versión que NO es SELF-CONTAINED?** Necesitás tener instalado el **.NET 8 Desktop Runtime** (buscá en Google "descargar .NET 8 Desktop Runtime"). La versión SELF-CONTAINED no requiere nada.

### Opción B: Con panel web (para administrar desde el navegador)

El panel web te deja administrar la sala desde cualquier navegador (incluso desde el celular).

1. Descargá **`sb0t-Pyxis-web-win-x64-SELF-CONTAINED.zip`** desde Releases.
2. Extraé el `.zip` y doble clic en **`sb0t.web.exe`**.
3. Se abre una **ventana negra (consola)** que te muestra la dirección y la contraseña:

```
==================================================
  sb0t Pyxis web panel: http://localhost:8080
  Login password: kl7pm2t12ecv
==================================================
```

4. Abrí tu navegador y entrá a **http://localhost:8080**
5. Escribí la contraseña que aparece en la consola y hacé clic en **Ingresar**.
6. ✅ ¡Listo! Ya estás en el panel de administración.

> 🔐 **Importante:** la contraseña se genera la primera vez. Podés cambiarla después en *Ajustes → Contraseña del panel web*. **No compartas esta contraseña** — es la llave de administración de tu sala.

> 💡 **Para conectarte desde otro dispositivo** (ej: tu celular en la misma red Wi-Fi), en vez de `localhost` usá la IP de tu PC, por ejemplo `http://192.168.1.50:8080`. Y para que la consola muestre la contraseña de nuevo, cerrá y volvé a abrir el programa.

---

## 🚀 Cómo compilar desde el código (para desarrolladores)

Si querés compilar el proyecto vos mismo:

### Requisitos
- **.NET 8 SDK** — descargalo de https://dotnet.microsoft.com/download/dotnet/8.0
- (Opcional) Visual Studio 2022 o VS Code

### Compilar todo
```bash
dotnet build sb0t5.sln
```

### Ejecutar la aplicación de escritorio
```bash
dotnet run --project gui
```

### Ejecutar el panel web
```bash
dotnet run --project sb0t.web
```
Después abrí http://localhost:8080 en el navegador.

### Publicar una versión (para Releases)

**Versión SELF-CONTAINED** (recomendada — el usuario no necesita instalar nada):
```bash
# Aplicación de escritorio para Windows 64 bits
dotnet publish gui -c Release -r win-x64 --self-contained true -o out/gui

# Panel web para Windows 64 bits
dotnet publish sb0t.web -c Release -r win-x64 --self-contained true -o out/web
```

**Versión framework-dependent** (más liviana — requiere .NET 8 Runtime instalado):
```bash
dotnet publish gui -c Release -r win-x64 --self-contained false -o out/gui-fd
dotnet publish sb0t.web -c Release -r win-x64 --self-contained false -o out/web-fd
```

Comprimí cada carpeta `out/...` en un `.zip` y subilos a GitHub Releases.

> 💡 Funciona en Windows, Linux y macOS. Para Linux/macOS, en los comandos de publish cambiá `win-x64` por `linux-x64` o `osx-x64`.

---

## 📁 ¿Dónde se guardan los datos?

Todo se guarda en una carpeta de datos de tu usuario:

| Sistema | Ruta |
|---|---|
| Windows | `%APPDATA%\sb0t\sb0t\` |
| Linux/macOS | `~/.config/sb0t/sb0t/` (o similar) |

Ahí vas a encontrar:
- `settings.json` — toda la configuración de la sala
- `Dat/` — cuentas, bans, ignores (base de datos)
- `Avatars/` — los avatares del servidor y por defecto
- `serverlog.txt` — el registro del servidor

> ⚠️ **Antes de actualizar o reinstalar**, guardate una copia de esa carpeta. Así no perdés tu configuración, bans ni cuentas.

---

## 🌐 Trabajo en conjunto: escritorio + web al mismo tiempo

¿Sabías que la aplicación de escritorio y el panel web **comparten los mismos datos**? Podés:

1. Iniciar la sala desde la aplicación de escritorio.
2. Y al mismo tiempo abrir el panel web para expulsar, banear y ver los logs.

Ambos programas leen y escriben los mismos archivos, así que ves todo sincronizado. Solo tené cuidado de **no iniciar la sala desde los dos a la vez** (el puerto ya estará ocupado).

---

## 🔒 Consejos de seguridad

- Cambiá la contraseña del panel web apenas puedas.
- No expongas el puerto 8080 a Internet a menos que lo necesites.
- La contraseña del **dueño de la sala** (la que usan los clientes Ares para ser admin) configurala en el panel: *Admin → Contraseña del propietario*.
- Si ponés el servidor en una máquina de Internet, activá el captcha en *Avanzado* para frenar bots.

---

## 🗂️ Versión original (referencia)

La GUI original de Windows (WPF, .NET Framework 4.7.2, con configuración en el Registro de Windows) está preservada como referencia histórica en [`gui/legacy_wpf`](gui/legacy_wpf). No forma parte del build actual — la GUI Avalonia la reemplaza.

---

## 📄 Licencia

Este proyecto usa la **GNU Affero General Public License v3.0 (AGPL-3.0)**. Mirá el archivo [LICENSE](LICENSE) para más detalles.

---

¡Gracias por usar **sb0t Pyxis**! ❤️ Si tenés dudas, abrí un *Issue* en GitHub.
