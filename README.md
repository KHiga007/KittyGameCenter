# KittyGameCenter

KittyGameCenter es una aplicación de escritorio para Windows que reúne tus juegos instalados en una biblioteca local con temática sakura. Desde cada tarjeta podés iniciar el juego o abrir su carpeta de instalación.

## Funciones

- Detecta juegos en Steam, Epic Games, GOG y Ubisoft Connect. También revisa el registro de Windows y carpetas habituales de otras plataformas.
- Permite agregar rutas de búsqueda y juegos manualmente.
- Muestra carátulas cuando están disponibles.
- Permite ordenar por nombre, filtrar o agrupar por plataforma y elegir entre tres tamaños de tarjeta.
- Guarda la biblioteca y la configuración en `%LOCALAPPDATA%\AkiNekoApps\KittyGameCenter`.

## Descargar y ejecutar

Descargá el ZIP para Windows desde [Releases](https://github.com/KHiga007/KittyGameCenter/releases/latest), extraelo y ejecutá KittyGameCenter.exe. Necesitás .NET Desktop Runtime 10 y Microsoft Edge WebView2 Runtime.

## Estructura del proyecto

La aplicación usa C#, WinForms y WebView2 con una estructura MVC.

| Carpeta | Contenido |
| --- | --- |
| [Models](KittyGameCenter/Models) | Juegos, resultados de escaneo y datos persistidos |
| [Views](KittyGameCenter/Views) | Ventana de Windows e interfaz en HTML, CSS y JavaScript |
| [Controllers](KittyGameCenter/Controllers) | Acciones de la biblioteca y comunicación con la interfaz |
| [Services](KittyGameCenter/Services) | Detección de juegos, caché de imágenes y migración de datos |
