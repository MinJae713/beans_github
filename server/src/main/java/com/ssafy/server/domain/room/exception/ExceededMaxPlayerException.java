package com.ssafy.server.domain.room.exception;

public class ExceededMaxPlayerException extends RuntimeException {
    public ExceededMaxPlayerException() {
        super("playerCount는 maxPlayers를 초과할 수 없습니다.");
    }
}
