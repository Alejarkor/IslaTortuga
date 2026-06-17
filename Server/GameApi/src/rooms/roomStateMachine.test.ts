import { describe, it, expect } from "vitest";
import { RoomStateMachine, RoomStateError } from "./roomStateMachine";

describe("RoomStateMachine", () => {
  it("permite el camino feliz", () => {
    expect(RoomStateMachine.canTransition("waiting", "ready_check")).toBe(true);
    expect(RoomStateMachine.canTransition("ready_check", "starting")).toBe(true);
    expect(RoomStateMachine.canTransition("starting", "in_game")).toBe(true);
    expect(RoomStateMachine.canTransition("in_game", "finished")).toBe(true);
  });

  it("rechaza una transición inválida (in_game -> waiting)", () => {
    expect(RoomStateMachine.canTransition("in_game", "waiting")).toBe(false);
    expect(() => RoomStateMachine.assertTransition("in_game", "waiting")).toThrow(RoomStateError);
  });

  it("rechaza saltarse estados (waiting -> in_game)", () => {
    expect(RoomStateMachine.canTransition("waiting", "in_game")).toBe(false);
  });

  it("marca finished y cancelled como terminales", () => {
    expect(RoomStateMachine.isTerminal("finished")).toBe(true);
    expect(RoomStateMachine.isTerminal("cancelled")).toBe(true);
    expect(RoomStateMachine.isTerminal("waiting")).toBe(false);
  });
});
