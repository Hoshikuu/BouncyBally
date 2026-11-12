# Bolas y comportamiento personalizado

Cada definición de bola enlaza un asset **Custom Behavior**. Sus parámetros se editan en el apartado **Optional custom behavior** del editor Bouncy Bally o directamente en `Assets/Balls/Behaviors`. Los prefabs nuevos comparten `BallSpecial`; ya no tienen clases distintas por tipo. Los assets guardan velocidad, tamaño, arco, contactos por pin, bonus, recompensa, habilidad y parámetros propios (química, carrera y planos).

Las once variantes tienen una skill de desbloqueo y un nodo enlazado en SampleScene. Golden, Constructor y Gambler solo aparecen como sustituciones aleatorias después de desbloquear su skill. Probabilidades iniciales por bola: 1 %, 2 % y 2 %, editables en cada comportamiento. No figuran en la selección manual; las ocho variantes normales usan el selector del Canvas existente. El árbol conserva las mejoras añadidas por el usuario.

La bola contacta el borde superior del sprite del pin, teniendo en cuenta su propio radio para evitar solapamiento.

## Gambling

El panel y su presentador están guardados y enlazados en SampleScene, bajo el Canvas. La rueda tiene diez sectores de igual tamaño: tres rojos ×0, cuatro morados ×1, dos azules ×5 y uno verde ×10. Cada sector tiene probabilidad del 10 %. La animación termina en el sector usado para calcular el pago. Solo se multiplica la recompensa de esa bola, sin apostar saldo anterior. Las ruletas simultáneas esperan turno y el pago se hace al resolverlas. Un reinicio cancela las bolas y libera la vista.

## Herramientas

Se eliminaron los scripts de checks, el panel Validate, sus botones, los validadores de autoría OnValidate y las comprobaciones de integridad al inicializar vistas. Se mantienen las reglas de juego (saldo, desbloqueos y estado de tanda) que controlan las compras y lanzamientos.

## Verificación del entorno

Se analizaron la sintaxis C#, el YAML de escena, las referencias locales, los enlaces a comportamientos, las skills y la distribución de sectores. El entorno no dispone del editor Unity: compilación, animación y aspecto visual requieren comprobación en el editor.
