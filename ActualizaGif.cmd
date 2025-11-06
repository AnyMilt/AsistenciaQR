@echo off
echo === ACTUALIZANDO REPOSITORIO ===

git add .
set /p msg=Nueva actualizacion: 
git commit -m "%msg%"
git push origin main
echo === CAMBIOS SUBIDOS CON EXITO ===
pause