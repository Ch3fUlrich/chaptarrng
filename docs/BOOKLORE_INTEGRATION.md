# BookLore integration

ChaptarrNG supports BookLore as an optional notification destination. BookLore
is an external app; ChaptarrNG does not require, install, or bundle it.

## Configure BookDrop uploads

In **Settings > Connect**, add **BookLore** and set its base URL, username, and
password. The default URL is `http://localhost:6060`. Use a BookLore account
with administrator or upload permission. The connection test checks that
BookLore accepts the credentials.

After an import, ChaptarrNG sends each imported file to BookLore's BookDrop
intake folder. Files are streamed from disk, and ChaptarrNG leaves the source
files in place. Uploads are sequential. If BookLore rejects a file, that
notification fails; files already sent remain in BookDrop and later files in
the same event are not sent.

BookLore applies its own supported-format and maximum-upload-size rules. The
BookLore v2.4.0 API source accepts `.pdf`, `.epub`, `.cbz`, `.cbr`, `.cb7`,
`.mobi`, `.azw3`, `.azw`, `.fb2`, `.m4b`, `.m4a`, `.mp3`, and `.opus`. Check the
rules for the BookLore version you run and its configured upload limit.

## API contract and URL handling

The client logs in with `POST /api/v1/auth/login` and the configured username
and password. It uses the returned `accessToken` as a bearer token for
`POST /api/v1/files/upload/bookdrop`, sending the file as multipart form field
`file`. A successful upload returns `200 OK` with an empty response body.

ChaptarrNG does not follow redirects for the login or upload requests. Set the
base URL to BookLore's final canonical address, including any reverse-proxy
path, so both API requests respond directly.

The request and upload behavior were verified against the official BookLore
v2.4.0 source at commit
[`f397e98c5ab021b02e8930ce278583af11637238`](https://github.com/booklore-app/booklore/tree/f397e98c5ab021b02e8930ce278583af11637238).
The live contract check authenticated, uploaded a file, received `200 OK`, and
verified the exact bytes in BookLore's BookDrop folder. BookLore v2.4.0 can
return an HTTP 400 data-conflict response when the same account logs in twice
within one second because its generated refresh tokens collide; ChaptarrNG
retries that conflict once after a short delay. It also retries server errors
once. BookLore reports invalid credentials as HTTP 400, which ChaptarrNG maps
to a credential-validation error without retrying.

## BookOrbit status

BookLore's repository identifies BookOrbit as its official successor and says
BookLore is entering maintenance mode. This integration targets BookLore's
API; compatibility with BookOrbit has not been verified. The
[retirement matrix](FORK_FEATURES.md#bookshelfng-parity-matrix) keeps the
BookLore behavior and the BookOrbit or legacy-user transition as open
upstream-retirement work.

## Upstream references

- [BookLore v2.4.0 upload controller](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/controller/FileUploadController.java)
- [BookLore v2.4.0 upload service](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/service/upload/FileUploadService.java)
- [BookLore v2.4.0 authentication controller](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/controller/AuthenticationController.java)
- [BookLore v2.4.0 API error mapping](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/exception/ApiError.java)
- [BookLore v2.4.0 data-conflict response](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/exception/GlobalExceptionHandler.java)
- [BookLore v2.4.0 accepted file extensions](https://github.com/booklore-app/booklore/blob/f397e98c5ab021b02e8930ce278583af11637238/booklore-api/src/main/java/org/booklore/model/enums/BookFileExtension.java)
- [BookLore repository and BookOrbit successor notice](https://github.com/booklore-app/booklore)
