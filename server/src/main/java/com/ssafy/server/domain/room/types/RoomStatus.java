package com.ssafy.server.domain.room.types;

import com.ssafy.server.domain.room.exception.ValidateRoomStatusException;
import lombok.Getter;

@Getter
public enum RoomStatus {
    WAITING("waiting"),
    COUNTDOWN("countdown"),
    PLAYING("playing"),
    GAMEOVER("gameover");
    private final String label;
    private RoomStatus(String label) {
        this.label = label;
    }

    public static void validateStatus(String status) {
        for (RoomStatus roomStatus : RoomStatus.values())
            if (roomStatus.getLabel().equals(status))
                return;
        throw new ValidateRoomStatusException();
    }
}
