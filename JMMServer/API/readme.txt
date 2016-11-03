date: 20161103

Model:
		- common <- models that are used to present user objects also they use each other, and not other models
		- core <- every model that is used for get/set server configuration or are related with lower level communication within Nancy

Module:
		- apiv1:
				- Binnary.cs <- JMMServiceImplementation (aka BinaryBlob Service) attempt to move that out of binary blob sending between client and server
				- Legacy.cs <- Legacy readonly module for support not rewriten clients, until most of them ain't rewriten this module need to stay
		- apiv2:
				- Auth.cs <- /api/auth - apikey handler (create, delete)
				- Common.cs <- every api call that dont belong to anyother module - essentialy all common used calls for end user
				- Core.cs <- server configuration related calls
				- Database.cs <- /api/db - database setup handler (get, set, check, start) essential for setup database before runnin any Auth request (FirstRun)
				- Dev.cs <- /api/dev only when running in DEBUG mode
				- Unauth.cs <- any command that use can invoke without apikey should be placed here (ex. /api/version)
				- Webui.cs <- /api/webui - everything related with webui except /api/dashboard (common) as it a agregate function
				- Webui_redirect.cs <- Redirect / request to webui/index.html

Negotiation: <- custom negotiation types

Response: <- custom responde types

Views: <- Views for Razor (or other ViewEngine for Nancy)

Bootstrapper.cs <- Nancy configuration file

NancyExtensions.cs <- Nancy Extension configuration file (to add more handlers)