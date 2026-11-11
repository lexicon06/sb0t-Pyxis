# sb0t Pyxis

**sb0t Pyxis** es un servidor de chat compatible con Ares, mantenido y modernizado sobre .NET 8. Permite crear tu propia sala, conectarla con clientes clásicos (Ares, cb0t) y modernos (MercuryX / inbizier web y móvil), y administrarla desde una interfaz de escritorio o desde un panel web.

Es la evolución moderna del clásico sb0t: multiplataforma, con interfaz gráfica (Avalonia), panel web (Blazor Server), configuración en archivos (sin registro de Windows), enlace entre salas y soporte para las funciones que usan los clientes actuales.

> Actualizado a fines de 2026.

---

## Qué ofrece

- **Sala de chat Ares** con soporte para clientes clásicos y clientes web/móviles modernos.
- **Interfaz de escritorio (GUI)** para Windows, Linux y macOS (Avalonia).
- **Panel web de administración** para gestionar la sala desde el navegador.
- **Vrooms (salas virtuales):** cada vroom es privada; lo que pasa en una vroom no se ve en otra. En salas linkeadas, el chat se comparte solo entre usuarios que están en la **misma vroom** en ambos lados.
- **Enlace entre servidores (linking) Hub/Leaf:** conecta varias salas entre sí para formar una red.
- **Funciones para clientes modernos (MercuryX / inbizier):** reacciones en sala y PM, historial, manejo de mensajes personalizados (`NOSUCH2`) y otras capacidades anunciadas por el servidor cuando el cliente las soporta (con compatibilidad hacia atrás para clientes legacy).
- **Administración:** ver usuarios conectados, expulsar/banear/desbanear, avatares, topic, niveles, captcha, filtros, registro (logs) y más.
- **Extensiones:** soporte para extensiones (por ejemplo, clima, audio, etc.).

---

## Descargas (recomendado: self-contained)

Las versiones listas para usar están en **Releases**:

**https://github.com/lexicon06/sb0t-Pyxis/releases**

Elegí las versiones **self-contained**: traen todo incluido (runtime .NET), no necesitás instalar Visual Studio ni .NET en la máquina donde lo vas a ejecutar. Solo descomprimir y abrir.

| Archivo (ejemplo) | Qué es | Sistema |
|---|---|---|
| `sb0t-Pyxis-gui-win-x64-self-contained.zip` | Aplicación de escritorio (GUI) | Windows 64 bits |
| `sb0t-Pyxis-web-win-x64-self-contained.zip` | Panel web de administración | Windows 64 bits |

> Cada release puede incluir variantes adicionales según lo publicado. Si tu sistema es distinto, podés compilar una build self-contained para tu plataforma (ver abajo).

---

## Cómo usarlo

### Opción A: Aplicación de escritorio (GUI)

1. Descargá el zip **self-contained** de la GUI para tu sistema desde Releases.
2. Extraé el contenido en una carpeta.
3. Ejecutá `sb0t.exe` (Windows) o el binario correspondiente.
4. Configurá nombre de sala, puerto y bot, y presioná **Iniciar servidor**.

### Opción B: Panel web de administración

1. Descargá el zip **self-contained** del panel web desde Releases.
2. Extraé el contenido y ejecutá `sb0t.web.exe` (Windows) o el binario correspondiente.
3. La consola mostrará la dirección y una contraseña de acceso, por ejemplo:

   ```
   sb0t Pyxis web panel: http://localhost:8080
   Login password: ********
   ```

4. Abrí la URL en tu navegador e ingresá con esa contraseña.
5. Cambiá la contraseña del panel en *Ajustes* cuando puedas. Para acceder desde otro dispositivo en tu red, usá la IP de la máquina en lugar de `localhost`.

> No inicies la sala desde la GUI y desde el panel web al mismo tiempo (el puerto quedará ocupado). Ambos comparten los mismos datos.

### Linux / macOS

Podés usar los binarios publicados para tu plataforma (si están disponibles en el release) o compilar una versión self-contained:

```bash
# Ejemplo: GUI en Linux x64
dotnet publish gui/gui.csproj -c Release -r linux-x64 --self-contained true -o out/gui-linux
chmod +x out/gui-linux/sb0t

# Ejemplo: panel web en Linux x64
dotnet publish sb0t.web/sb0t.web.csproj -c Release -r linux-x64 --self-contained true -o out/web-linux
chmod +x out/web-linux/sb0t.web
```

En macOS, reemplazá `linux-x64` por `osx-x64` (Intel) u `osx-arm64` (Apple Silicon), según corresponda.

---

## Compilar desde el código

Requisitos: **.NET 8 SDK** (https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
# Compilar todo
dotnet build sb0t5.sln -c Release

# Ejecutar GUI o panel web en desarrollo
dotnet run --project gui
dotnet run --project sb0t.web
```

Publicar builds self-contained (sin dependencias externas):

```bash
# Windows x64 - GUI
dotnet publish gui/gui.csproj -c Release -r win-x64 --self-contained true -o out/gui-win

# Windows x64 - Panel web
dotnet publish sb0t.web/sb0t.web.csproj -c Release -r win-x64 --self-contained true -o out/web-win
```

Comprimí cada carpeta de salida en un `.zip` para distribuirla.

---

## Dónde se guardan los datos

La configuración y los datos se guardan en la carpeta de datos del usuario:

| Sistema | Ruta aproximada |
|---|---|
| Windows | `%APPDATA%\sb0t\sb0t\` |
| Linux / macOS | `~/.config/sb0t/sb0t/` (según entorno) |

Ahí vas a encontrar, entre otros: `settings.json` (configuración), `Dat/` (cuentas, bans, ignores), `Avatars/`, y registros del servidor.

> Antes de actualizar o reinstalar, hacé una copia de esa carpeta para no perder configuración, bans ni cuentas.

---

## Seguridad básica

- Cambiá la contraseña del panel web luego del primer inicio.
- No expongas el panel web a Internet si no es necesario.
- Configurá la contraseña de propietario/admin de la sala para los clientes.
- Si el servidor es público, considerá activar captcha y revisar las opciones de protección/red según tu entorno.

---

## Licencia

Este proyecto se distribuye bajo la **GNU Affero General Public License v3.0 (AGPL-3.0)**. Ver [LICENSE](LICENSE).

---

Proyecto mantenido como **sb0t Pyxis**. Para reportes o sugerencias, abrí un *Issue* en GitHub.
