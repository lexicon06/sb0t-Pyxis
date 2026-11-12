# Contribuir a sb0t Pyxis

¡Gracias por tu interés en contribuir!

## Cómo contribuir

- **Reportes de bugs y sugerencias:** abrí un [Issue](https://github.com/lexicon06/sb0t-Pyxis/issues) describiendo el problema, los pasos para reproducirlo, el sistema operativo y la versión que estás usando.
- **Cambios de código:** hacé un fork, creá una rama para tu cambio y enviá un Pull Request contra `main` con una descripción clara de qué hace y por qué.

## Desarrollo local

Requisitos:
- **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0

Compilar:

```bash
dotnet build sb0t5.sln -c Release
```

Ejecutar en desarrollo:

```bash
# GUI de escritorio
dotnet run --project gui

# Panel web
dotnet run --project sb0t.web
```

## Guías

- Mantené los cambios enfocados y coherentes con el estilo y la estructura existente del proyecto.
- Evitá introducir cambios que rompan la compatibilidad con clientes clásicos cuando sea posible.
- Si tu cambio afecta el comportamiento visible o el uso, actualizá la documentación correspondiente (por ejemplo, el README).

## Licencia

Al contribuir, aceptás que tus aportes se distribuyan bajo la licencia del proyecto ([AGPL-3.0](LICENSE)).
