FROM alpine
RUN apk update && apk add --no-cache go
RUN mkdir /app
RUN mkdir /app/db
RUN mkdir /app/files
WORKDIR /app
ADD . /app
ENV GIN_MODE=release
RUN go build -o main .
CMD ["/app/main"]