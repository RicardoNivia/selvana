export interface Seccion {
  titulo: string
  parrafos: string[]
}

export interface Documento {
  titulo: string
  resumen: string
  secciones: Seccion[]
}

export const actualizado = '22 de octubre de 2026'

/** Textos preliminares: los revisa y aprueba el área jurídica antes de salir a producción. */
export const documentos: Record<string, Documento> = {
  'tratamiento-de-datos': {
    titulo: 'Política de tratamiento de datos personales',
    resumen: 'Cómo Selvana recoge, usa, guarda y protege los datos de candidatos, empleados y usuarios, según la Ley 1581 de 2012 y el Decreto 1377 de 2013.',
    secciones: [
      { titulo: '1. Responsables y encargados', parrafos: [
        'La empresa que publica la vacante o que contrata a la persona es la responsable del tratamiento de sus datos. Selvana actúa como encargada: trata los datos por cuenta de esa empresa y según sus instrucciones.',
        'Los datos de los usuarios de la consola (personas de RRHH, líderes y administradores) los trata Selvana como responsable, para prestar el servicio.',
      ] },
      { titulo: '2. Qué datos tratamos', parrafos: [
        'Datos de la hoja de vida y de contacto: nombre, documento, celular, correo, ciudad, experiencia, formación, cursos, habilidades y perfil de LinkedIn si la persona lo comparte; respuestas a las preguntas filtro y resultados de pruebas.',
        'No extraemos ni usamos para evaluar a nadie la edad, la fecha de nacimiento, el género, el estado civil, los hijos, la religión, la foto ni la dirección exacta, aunque aparezcan en la hoja de vida.',
        'Los datos sensibles (salud, pruebas psicológicas) se cifran, se muestran enmascarados y solo los ve quien tiene el rol autorizado.',
      ] },
      { titulo: '3. Para qué los usamos', parrafos: [
        'Gestionar procesos de selección y vinculación, contactar a la persona sobre sus postulaciones, ofrecerle vacantes similares si lo autoriza, cumplir obligaciones legales y mejorar la calidad del servicio con datos anonimizados.',
        'La inteligencia artificial de Selvana lee hojas de vida y sugiere puntajes con su explicación. Toda decisión que afecte a una persona la toma un ser humano, y cada sugerencia y decisión queda registrada.',
      ] },
      { titulo: '4. Autorización', parrafos: [
        'Antes de enviar una postulación, la persona acepta de forma expresa la autorización de tratamiento. Guardamos como evidencia la versión del texto aceptado, la fecha, la dirección IP, el navegador y una huella criptográfica que permite comprobar que no se alteró.',
      ] },
      { titulo: '5. Derechos del titular', parrafos: [
        'Conocer, actualizar y rectificar sus datos; pedir prueba de la autorización; ser informado del uso que se les da; revocar la autorización o pedir la supresión cuando no haya un deber legal de conservarlos; y presentar quejas ante la Superintendencia de Industria y Comercio.',
        'Las consultas se responden en máximo 10 días hábiles y los reclamos en máximo 15 días hábiles, como lo establece la Ley 1581 de 2012.',
      ] },
      { titulo: '6. Cómo ejercer sus derechos', parrafos: [
        'Desde el formulario «Tus datos personales» del portal de empleo o escribiendo a la empresa responsable. Cada solicitud queda radicada con su plazo legal y su respuesta.',
      ] },
      { titulo: '7. Seguridad y conservación', parrafos: [
        'Las hojas de vida se guardan cifradas, cada apertura queda en una bitácora de auditoría con su motivo y la bitácora está encadenada para detectar cualquier alteración. Los datos se conservan mientras dure la finalidad o lo exija la ley.',
      ] },
    ],
  },
  'terminos': {
    titulo: 'Términos y condiciones de uso',
    resumen: 'Las reglas para usar la consola de Selvana y el portal de empleo.',
    secciones: [
      { titulo: '1. El servicio', parrafos: [
        'Selvana es un software de talento humano con inteligencia artificial que se presta como servicio en la nube a empresas. El portal de empleo es público y gratuito para los candidatos.',
      ] },
      { titulo: '2. Cuentas y acceso', parrafos: [
        'Cada empresa administra sus usuarios y sus roles. La persona es responsable de guardar su contraseña y de lo que se haga con su cuenta. Los permisos de cada rol los define la matriz de permisos de la empresa.',
      ] },
      { titulo: '3. Uso de la inteligencia artificial', parrafos: [
        'Las sugerencias de la IA son una ayuda, no una decisión. Descartar, contratar, sancionar o calificar a una persona siempre requiere la aprobación de alguien de la empresa.',
      ] },
      { titulo: '4. Uso aceptable', parrafos: [
        'No se permite publicar vacantes con requisitos discriminatorios, usar el servicio para fines distintos a la gestión de talento humano ni intentar acceder a datos de otras empresas.',
      ] },
      { titulo: '5. Propiedad intelectual', parrafos: [
        'El software, la marca y los contenidos de Selvana están protegidos. Los datos que carga cada empresa son de esa empresa.',
      ] },
      { titulo: '6. Ley aplicable', parrafos: ['Estos términos se rigen por las leyes de la República de Colombia.'] },
    ],
  },
  'aviso-de-privacidad': {
    titulo: 'Aviso de privacidad',
    resumen: 'El resumen de lo esencial sobre sus datos personales en Selvana.',
    secciones: [
      { titulo: 'En pocas palabras', parrafos: [
        'Sus datos los trata la empresa a la que se postula, con Selvana como encargada, para el proceso de selección y, si lo autoriza, para ofrecerle vacantes similares.',
        'Nunca usamos su edad, género, estado civil, foto ni dirección exacta para evaluarle. La IA sugiere y una persona decide.',
        'Puede conocer, actualizar, rectificar o pedir que se supriman sus datos cuando quiera, desde «Tus datos personales» en el portal de empleo.',
        'La política completa está en «Política de tratamiento de datos personales».',
      ] },
    ],
  },
}
