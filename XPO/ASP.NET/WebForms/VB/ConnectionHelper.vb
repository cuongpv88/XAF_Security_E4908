﻿Imports DevExpress.ExpressApp
Imports DevExpress.ExpressApp.Security
Imports DevExpress.ExpressApp.Security.ClientServer
Imports DevExpress.Persistent.BaseImpl.PermissionPolicy
Imports BusinessObjectsLibrary.BusinessObjects

Namespace WebFormsApplication
    Public NotInheritable Class ConnectionHelper

        Private Sub New()
        End Sub

        Public Shared Function GetObjectSpaceProvider(ByVal security As SecurityStrategyComplex) As SecuredObjectSpaceProvider
            Dim objectSpaceProvider As New SecuredObjectSpaceProvider(security, XpoDataStoreProviderService.GetDataStoreProvider(Nothing, True), True)
            RegisterEntities(objectSpaceProvider)
            Return objectSpaceProvider
        End Function
        Public Shared Function GetSecurity(ByVal authenticationName As String, ByVal parameter As Object) As SecurityStrategyComplex
            Dim authentication As New AuthenticationMixed()
            authentication.LogonParametersType = GetType(AuthenticationStandardLogonParameters)
            authentication.AddAuthenticationStandardProvider(GetType(PermissionPolicyUser))
            authentication.AddIdentityAuthenticationProvider(GetType(PermissionPolicyUser))
            authentication.SetupAuthenticationProvider(authenticationName, parameter)
            Dim security As New SecurityStrategyComplex(GetType(PermissionPolicyUser), GetType(PermissionPolicyRole), authentication)
            security.RegisterXPOAdapterProviders()
            Return security
        End Function
        Public Shared Sub RegisterEntities(ByVal objectSpaceProvider As IObjectSpaceProvider)
            objectSpaceProvider.TypesInfo.RegisterEntity(GetType(Employee))
            objectSpaceProvider.TypesInfo.RegisterEntity(GetType(PermissionPolicyUser))
            objectSpaceProvider.TypesInfo.RegisterEntity(GetType(PermissionPolicyRole))
        End Sub
    End Class
End Namespace