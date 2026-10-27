import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { Bandeja } from '../features/bandeja/Bandeja'
import { BancoTalento } from '../features/candidatos/BancoTalento'
import { FichaCandidato } from '../features/candidatos/FichaCandidato'
import { CargoDetalle, CargoNuevo } from '../features/estructura/CargoPaginas'
import { ConectarNomina } from '../features/estructura/ConectarNomina'
import { EstructuraCargos } from '../features/estructura/EstructuraCargos'
import { ActivarCuenta } from '../features/inicio/ActivarCuenta'
import { Ingresar } from '../features/inicio/Ingresar'
import { Legal } from '../features/inicio/Legal'
import { RequiereSesion } from '../features/sesion/RequiereSesion'
import { Inicio } from '../features/inicio/Inicio'
import { PortalLayout } from '../features/portal/PortalLayout'
import { MisDatos } from '../features/portal/MisDatos'
import { PortalListado } from '../features/portal/PortalListado'
import { PortalVacante } from '../features/portal/PortalVacante'
import { Postular } from '../features/portal/Postular'
import { ConversacionCandidato } from '../features/preseleccion/ConversacionCandidato'
import { PresentarPruebas } from '../features/pruebas/PresentarPruebas'
import { PrivacidadAuditoria } from '../features/privacidad/PrivacidadAuditoria'
import { UsuariosRoles } from '../features/usuarios/UsuariosRoles'
import { VacanteDetalle } from '../features/vacantes/VacanteDetalle'
import { VacanteNueva } from '../features/vacantes/VacanteNueva'
import { VacantesLista } from '../features/vacantes/VacantesLista'
import { OfertaCandidato } from '../features/vinculacion/OfertaCandidato'
import { ProcesoVinculacion } from '../features/vinculacion/ProcesoVinculacion'
import { ProcesosVinculacion } from '../features/vinculacion/ProcesosVinculacion'
import { Layout } from './Layout'

const router = createBrowserRouter([
  // Público: página comercial, inicio de sesión y portal de empleo
  { path: '/', element: <Inicio /> },
  { path: '/ingresar', element: <Ingresar /> },
  { path: '/legal/:documento', element: <Legal /> },
  { path: '/legal', element: <Navigate to="/legal/tratamiento-de-datos" replace /> },
  { path: '/activar/:token', element: <ActivarCuenta /> },
  {
    path: '/empleos',
    element: <PortalLayout />,
    children: [
      { index: true, element: <PortalListado /> },
      { path: 'mis-datos', element: <MisDatos /> },
      { path: ':codigo', element: <PortalVacante /> },
      { path: ':codigo/postular', element: <Postular /> },
    ],
  },
  // El candidato presenta sus pruebas con su enlace, sin cuenta
  { path: '/pruebas', element: <PortalLayout />, children: [{ path: ':token', element: <PresentarPruebas /> }] },
  // El candidato conversa con el asistente de preselección con su enlace, sin cuenta
  { path: '/preseleccion', element: <PortalLayout />, children: [{ path: ':token', element: <ConversacionCandidato /> }] },
  // El candidato lee y acepta su oferta laboral con su enlace, sin cuenta
  { path: '/vinculacion', element: <PortalLayout />, children: [{ path: ':token', element: <OfertaCandidato /> }] },
  // Consola de RRHH (requiere sesión)
  {
    path: '/consola',
    element: <RequiereSesion><Layout /></RequiereSesion>,
    children: [
      { index: true, element: <Navigate to="/consola/bandeja" replace /> },
      { path: 'bandeja', element: <Bandeja /> },
      { path: 'vacantes', element: <VacantesLista /> },
      { path: 'vacantes/nueva', element: <VacanteNueva /> },
      { path: 'vacantes/:id', element: <VacanteDetalle /> },
      { path: 'candidatos', element: <BancoTalento /> },
      { path: 'candidatos/:id', element: <FichaCandidato /> },
      { path: 'vinculacion', element: <ProcesosVinculacion /> },
      { path: 'vinculacion/:id', element: <ProcesoVinculacion /> },
      { path: 'empresa/conectar-nomina', element: <ConectarNomina /> },
      { path: 'empresa/estructura', element: <EstructuraCargos /> },
      { path: 'empresa/estructura/cargos/nuevo', element: <CargoNuevo /> },
      { path: 'empresa/estructura/cargos/:id', element: <CargoDetalle /> },
      { path: 'empresa/usuarios', element: <UsuariosRoles /> },
      { path: 'empresa/privacidad', element: <PrivacidadAuditoria /> },
    ],
  },
  { path: '/empresa/*', element: <Navigate to="/consola/empresa/estructura" replace /> },
  { path: '*', element: <Navigate to="/" replace /> },
])

export function App() {
  return <RouterProvider router={router} />
}
