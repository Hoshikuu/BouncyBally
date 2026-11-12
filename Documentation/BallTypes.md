# Tipos de bolas

Los tipos nuevos están en `Assets/Scripts/Balls/Types`, con un prefab y un asset por tipo. El botón «Tipos de bolas» permite asignarlos a las tandas desbloqueadas cuando están listas. Se ofrecen desbloqueados para probar sus funciones sin cambiar las habilidades. La básica y las variantes existentes se conservan.

- Beach: mayor tamaño, mitad de velocidad, recompensa x2.
- Basket: arco de rebote más alto, menor velocidad, +0,25 de bonus por contacto.
- Trick: dirección inicial elegible en el selector; los demás giros siguen las probabilidades del tablero.
- Sticky: tres contactos animados con cada pin, cada uno aplicando el bonus.
- Golden: recompensa x10, identidad dorada; 1 % de reemplazar cada lanzamiento.
- Steel: mayor tamaño, rebote bajo, velocidad x0,2 y recompensa x3.
- Ghost: tamaño x0,6, velocidad x2 y recompensa x0,5. Dupliball queda pendiente con los efectos especiales.
- Chemical: cada tercer contacto añade +0,5 a otra bola activa aleatoria, o a sí misma si no hay otra.
- Marble: velocidades aleatorias entre x0,8 y x1,4; la primera llegada de la tanda recibe x2.
- Gambler: una casilla verde y nueve rojas/negras, equiprobables. La ruleta animada se muestra al llegar al slot: verde paga x10, el resto cero. No afecta al saldo previo. Las ruletas simultáneas esperan turno y la tanda espera su resolución.
- Constructor: sombrero amarillo y un plano por llegada; 2 % de reemplazar cada lanzamiento (además del 1 % dorado). Los planos se guardan en PlayerPrefs con clave propia. Su uso en paredes queda fuera de las funciones de bolas.

Los parámetros no definidos en el diseño son provisionales. No se implementa Egg ni se cambian nodos, precios, mejoras, core shards o efectos especiales.

## Validación

En Unity: `Tools > Bouncy Bally > Check new ball types` comprueba definiciones, componentes, sectores de apuesta, Sticky y Trick. Abrir SampleScene y probar cada variante desde el selector; verificar que la tanda termina, que una pérdida no descuenta saldo previo y que cambiar o reiniciar durante una ruleta no abona pagos pendientes. Comprobar carreras con varias Marble y aparición de planos al llegar una Constructor.

En este entorno solo se verificaron referencias nuevas, metadatos y `git diff --check`. Las referencias de recursos compartidos del prefab original se conservaron. No hay editor Unity disponible para compilar ni ejecutar estas comprobaciones.
