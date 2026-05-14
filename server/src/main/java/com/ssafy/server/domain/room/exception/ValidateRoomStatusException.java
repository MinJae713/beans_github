package com.ssafy.server.domain.room.exception;

public class ValidateRoomStatusException extends RuntimeException {
    public ValidateRoomStatusException() {
        super("status는 waiting, countdown, playing, gameover 중 하나여야 합니다.");
    }
}
