#!/bin/bash
set -e

# Detectar ruta de sqlcmd en imágenes oficiales de Microsoft
if [ -f /opt/mssql-tools18/bin/sqlcmd ]; then
    SQLCMD="/opt/mssql-tools18/bin/sqlcmd -C"
elif [ -f /opt/mssql-tools/bin/sqlcmd ]; then
    SQLCMD="/opt/mssql-tools/bin/sqlcmd"
else
    SQLCMD="sqlcmd -C"
fi

echo "=============================================="
echo "GimnasioDB - Script de Inicialización Automática"
echo "=============================================="

echo "Esperando que SQL Server acepte conexiones..."
until $SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -Q "SELECT 1" > /dev/null 2>&1; do
    echo "SQL Server no está listo todavía. Reintentando en 2 segundos..."
    sleep 2
done

echo "SQL Server está operativo. Verificando existencia de GimnasioDB..."
DB_EXISTS=$($SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(1) FROM sys.databases WHERE name = 'GimnasioDB'")

if [ "$DB_EXISTS" = "1" ]; then
    echo "GimnasioDB ya existe. No es necesario re-ejecutar los scripts de inicialización."
    exit 0
fi

echo "[1/5] Ejecutando Creacion de BBDD.sql (Tablas y Restricciones)..."
$SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -i "/scripts/Creacion de BBDD.sql"

echo "[2/5] Ejecutando Insercion de Datos.sql (Datos semilla)..."
$SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -i "/scripts/Insercion de Datos.sql"

echo "[3/5] Ejecutando Vistas.sql..."
$SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -i "/scripts/Vistas.sql"

echo "[4/5] Ejecutando Procedimientos.sql..."
$SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -i "/scripts/Procedimientos.sql"

echo "[5/5] Ejecutando Triggers.sql..."
$SQLCMD -S sqlserver -U sa -P "BaseDatos#2" -i "/scripts/Triggers.sql"

echo "=========================================================="
echo "¡GimnasioDB fue creada e inicializada exitosamente!"
echo "=========================================================="
