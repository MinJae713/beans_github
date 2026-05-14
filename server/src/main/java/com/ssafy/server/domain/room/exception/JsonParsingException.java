package com.ssafy.server.domain.room.exception;

public class JsonParsingException extends IllegalStateException {
    public JsonParsingException(String message, Throwable e) {
        super(message, e);
    }
}
