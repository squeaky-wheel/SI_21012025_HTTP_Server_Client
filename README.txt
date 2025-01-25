The server is programmed against NET 8.
As the result - it requires the framework, of the appropriate version, to compile, and the runtime to execute.

The client-related functionality of the system is intended, from the point of view of the process in this file, to be verified using Postman.
Unfortunately, at the time of the creation of this file, Postman does not support exporting of the collections containing WebSocket requests. As the result, the request will have to be created as part of the process.

Since one of the requirements is the prevention of the disruption of 'work' units, as if the work is performed by a hardware resource, capable of handling a single request at a time - the 'work' endpoint is limited to a single request at a time.
Considering the above requirement, the server was created, using the least effort approach - to handle just one client listening to the outgoing messages at a time too. As having multiple clients listening to the messages would require an implementation of a mechanism indicating which client is making HTTP requests in order to distinguish which WebSocket to transmit the messages to.
Attempts to exceed the limits result in the appropriate status code being returned.

The server exposes the following endpoints:
	/messages
		The endpoint supports WebSocket (GET) requests.
		Returns the following status codes:
			400 - when a non-WebSocket request is made to the endpoint.
			503 - when the server is busy processing another request sent to the endpoint.
			WebSocket-specific status codes such as 101, 1000, 1006 and, potentially, others.

	/server/ping
		The endpoint supports HTTP (POST) requests.
		Returns the following status codes:
			200 - when the WebSocket message, acting as the response to a 'ping' request, is successfully transmitted.
			400 - when nobody is listening to the messages via a WebSocket.
			408 - when a request gets cancelled.
			500 - when an unexpected boundary condition is met.

	/work/start
		The endpoint supports HTTP (POST) requests.
		Returns the following status codes:
			200 - when WebSocket messages, concerning the start and completion of work, are successfully transmitted and the 'work' is done.
			400 - when nobody is listening to the messages via a WebSocket.
			408 - when a request gets cancelled.
			500 - when an unexpected boundary condition is met.
			503 - when the server is busy processing another request sent to the endpoint.

To verify the functionality:
1) Compile the server using the following command.
dotnet build -c Release .\server\Server.sln

2) Navigate to the directory of the compilation output and run the following command to host the server using a free port.
Server --urls "http://[::1]:0"

3) Take note of the port, the server will declare to be listening on. The message will look similar to the one below.
Now listening on: http://[::1]:59827

4) Open Postman and create a WebSocket request.
Enter the servers' socket, EXCLUDING the protocol part, into the requests' URL text box.
Append /messages to the entry.
The result is intended to look like the line below.
localhost:59827/messages

5) Navigate to the Client directory of the repository to locate the collection named 'HTTP requests.postman_collection.json'.
Import the collection into Postman.
Modify the imported collections' variable named 'server_socket' to refer to the socket the server is listening on.
The result is intended to look like the line below.
http://localhost:59827

6) Make the WebSocket request by clicking the 'Connect' button.
The system is now ready for the validation of the functionality, related to pinging and starting work, as defined by the requirements.
The requirements can be found in the appropriate files located in the root of the repository.
