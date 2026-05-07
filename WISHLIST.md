# Security Monitor - Wishlist & Futuro

Este documento detalla las posibles mejoras y funcionalidades futuras para el sistema **Security Monitor** (Soldado & Comandante), basadas en el análisis actual de la arquitectura y las necesidades detectadas.

## 📍 Geolocalización y Precisión
- [ ] **Soporte para Múltiples Zonas Seguras**: Permitir que un soldado tenga más de una ubicación permitida (ej. Oficina Principal, Sucursal, Home Office).
- [ ] **Triangulación por Wi-Fi (BSSID)**: Mejorar el fallback de IP utilizando el escaneo de redes Wi-Fi cercanas (más preciso que la triangulación por IP de `ipwhois`).
- [ ] **Reset Remoto de "Zona Segura"**: Funcionalidad para que el Comandante pueda ordenar al Soldado borrar su `OriginLatitude/Longitude` y capturar una nueva sede base.
- [ ] **Log de Proveedor de Ubicación**: Registrar si la ubicación se obtuvo por GPS Nativo o por IP para evaluar el nivel de confianza de las alarmas.

## 🔒 Seguridad y Robustez
- [x] **Configuración Encriptada (AES-256)**: `appsettings.json` protegido con encriptación vinculada al hardware.
- [x] **Identidad de Hardware (HWID por MAC)**: Soldado identificado por MAC Address de fábrica + BIOS Serial + MachineGuid. Imposible de falsificar.
- [x] **Tráfico Encriptado (Cisco IPsec)**: Heartbeats enviados con AES-256 + HMAC-SHA256 + Anti-Replay.
- [ ] **Modo Servicio Robusto**: Asegurar que el proceso del Soldado se reinicie automáticamente si es finalizado (Watchdog).
- [ ] **Detección de Manipulación**: Alertar si el archivo de configuración es modificado fuera de la aplicación.

## 📊 Dashboard y Monitoreo (Comandante)
- [ ] **Mapa Interactivo**: Integrar OpenStreetMap/Leaflet en el Dashboard para ver la ubicación real de todos los soldados en un mapa en tiempo real.
- [ ] **Alertas en Tiempo Real (SignalR)**: Eliminar el refresco de 5 segundos y usar WebSockets para que las alertas aparezcan instantáneamente.
- [ ] **Notificaciones Externas**: Envío de alertas por Telegram, Email o WhatsApp cuando un soldado sale de la geocerca.
- [ ] **Histórico de Ubicaciones**: Guardar un historial de dónde ha estado el soldado (Migas de pan / Breadcrumbs).

## 🛠️ Administración y Control
- [ ] **Configuración Centralizada**: Que el Soldado descargue el "Radio Permitido" y el "SSID Autorizado" desde el Comandante en lugar de depender de su JSON local.
- [ ] **Comandos Remotos**:
    - [ ] Captura de pantalla al activarse la alarma.
    - [ ] Mensaje emergente de advertencia al usuario.
    - [ ] Bloqueo preventivo de sesión.
- [ ] **Auto-Update**: Capacidad del Soldado para actualizarse automáticamente cuando haya una nueva versión en el servidor.

## 📝 Notas sobre la implementación actual
Actualmente, el sistema utiliza una **"Auto-configuración de Sede"** (en `AgentWorker.cs`) que fija la ubicación la primera vez que se ejecuta. 
* **Ventaja**: Facilidad de despliegue ("instalar y listo").
* **Riesgo**: Si el GPS falla en el primer arranque y usa una IP inexacta, la "Sede Base" quedará mal configurada permanentemente hasta que se edite el JSON. Se recomienda implementar la validación de precisión o el reset remoto mencionado arriba.
