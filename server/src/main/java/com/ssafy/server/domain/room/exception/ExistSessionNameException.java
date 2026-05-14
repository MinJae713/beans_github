package com.ssafy.server.domain.room.exception;

public class ExistSessionNameException extends RuntimeException {
    public ExistSessionNameException(String sessionName) {
        super("이미 존재하는 sessionName입니다: " + sessionName);
    }
}
