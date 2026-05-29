# Cloud Commander - Guía de Despliegue (PythonAnywhere)

Este es el servidor centralizado para el sistema **Security Monitor**. Permite recibir alertas de cualquier "Soldado" conectado a internet.

## Pasos para desplegar en PythonAnywhere:

1. **Crear Cuenta**: Regístrate en [PythonAnywhere](https://www.pythonanywhere.com/).
2. **Subir Archivos**: Clona este repositorio o sube el contenido de esta carpeta (`app.py`, `templates/`, `requirements.txt`) a tu directorio principal en la nube.
3. **Crear Entorno**: Abre una consola Bash en PythonAnywhere y ejecuta:
   ```bash
   mkvirtualenv --python=/usr/bin/python3.10 myenv
   pip install -r requirements.txt
   ```
4. **Configurar Web App**:
   - Ve a la pestaña **Web** y crea una nueva app usando **Flask** y **Python 3.10**.
   - En la sección "Code", apunta el "WSGI configuration file" a tu archivo `app.py`.
   - Asegúrate de activar el Virtualenv creado en el paso 3.
5. **Listo**: Tu servidor estará escuchando en `tuusuario.pythonanywhere.com`.

## Integración con el Soldado C#:
En el archivo `appsettings.json` del soldado, cambia la URL a:
`https://tuusuario.pythonanywhere.com/api/heartbeat`
