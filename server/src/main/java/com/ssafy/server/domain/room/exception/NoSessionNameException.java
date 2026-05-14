package com.ssafy.server.domain.room.exception;

public class NoSessionNameException extends RuntimeException {
    public NoSessionNameException(String sessionName) {
        super("존재하지 않는 sessionName입니다: " + sessionName);
    }
}
