FROM alpine
RUN apk update && apk add --no-cache go
WORKDIR /app
ADD . /app
ENV GIN_MODE=release
RUN go build -o main .
CMD ["/app/main"]