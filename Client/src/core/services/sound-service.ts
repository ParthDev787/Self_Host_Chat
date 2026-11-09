import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SoundService {
  private audioCtx: AudioContext | null = null;
  private isMuted = false;

  private getAudioContext(): AudioContext | null {
    if (typeof window === 'undefined') return null;
    try {
      const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioContextClass) return null;
      if (!this.audioCtx) {
        this.audioCtx = new AudioContextClass();
      }
      if (this.audioCtx.state === 'suspended') {
        this.audioCtx.resume().catch(() => {});
      }
      return this.audioCtx;
    } catch {
      return null;
    }
  }

  /**
   * Triggers device haptic vibration on supported devices (Android, Chrome, Safari iOS).
   * @param pattern Vibration pattern in ms (e.g. 100 for single pulse, [120, 60, 180] for double pulse)
   */
  vibrate(pattern: number | number[] = [120, 60, 180]) {
    if (this.isMuted || typeof window === 'undefined') return;
    try {
      if ('navigator' in window && typeof navigator.vibrate === 'function') {
        navigator.vibrate(pattern);
      }
    } catch {}
  }

  /**
   * Plays a warm, organic Telegram/WhatsApp style "water-drop pop"
   * and triggers a subtle haptic vibration click when a message is sent.
   */
  playSentSound() {
    // 1. Trigger light tactile haptic vibration (35ms tap)
    this.vibrate(35);

    if (this.isMuted) return;
    try {
      const ctx = this.getAudioContext();
      if (!ctx) return;

      const now = ctx.currentTime;

      // Acoustic Lowpass Filter to remove robotic harshness
      const filter = ctx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.setValueAtTime(2200, now);
      filter.connect(ctx.destination);

      // Bubble pop oscillator: quick upward acoustic sweep
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(380, now);
      osc.frequency.exponentialRampToValueAtTime(940, now + 0.05);

      // Fast, snappy percussive envelope
      gain.gain.setValueAtTime(0.001, now);
      gain.gain.linearRampToValueAtTime(0.60, now + 0.005);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.085);

      osc.connect(gain);
      gain.connect(filter);

      osc.start(now);
      osc.stop(now + 0.09);
    } catch {}
  }

  /**
   * Plays a premium Apple / Slack style Crystal Marimba chime (D5 -> F#5 -> C#6)
   * and triggers a noticeable double-pulse haptic vibration when an incoming message drops in.
   */
  playNotificationSound() {
    // 1. Trigger distinct double-pulse haptic vibration on message arrival (120ms vibrate, 60ms pause, 180ms vibrate)
    this.vibrate([120, 60, 180]);

    if (this.isMuted) return;
    try {
      const ctx = this.getAudioContext();
      if (!ctx) return;

      const now = ctx.currentTime;

      // Master dynamics compressor for maximum warmth and loudness without speaker distortion
      const compressor = ctx.createDynamicsCompressor();
      compressor.threshold.setValueAtTime(-10, now);
      compressor.knee.setValueAtTime(8, now);
      compressor.ratio.setValueAtTime(4, now);
      compressor.attack.setValueAtTime(0.002, now);
      compressor.release.setValueAtTime(0.20, now);
      compressor.connect(ctx.destination);

      // Harmonic Triad (Warm Major 7th Bell Chime)
      // Note 1: 587.33 Hz (D5) - Soft intro tone
      this.playAcousticNote(ctx, 587.33, now, 0.35, 0.50, compressor);

      // Note 2: 739.99 Hz (F#5) - Sweet melodic step
      this.playAcousticNote(ctx, 739.99, now + 0.07, 0.45, 0.58, compressor);

      // Note 3: 1108.73 Hz (C#6) - Resonant crystal bell strike that rings out
      this.playAcousticNote(ctx, 1108.73, now + 0.15, 0.65, 0.70, compressor);
    } catch {}
  }

  /**
   * Synthesizes an acoustic marimba / celesta bell note with warm body and glassy shimmer.
   */
  private playAcousticNote(
    ctx: AudioContext,
    freq: number,
    startTime: number,
    duration: number,
    volume: number,
    dest: AudioNode
  ) {
    // 1. Fundamental warm body (pure sine)
    const osc1 = ctx.createOscillator();
    const gain1 = ctx.createGain();
    osc1.type = 'sine';
    osc1.frequency.setValueAtTime(freq, startTime);

    // 2. Crystal harmonic overtone (triangle octave) for clarity and presence
    const osc2 = ctx.createOscillator();
    const gain2 = ctx.createGain();
    osc2.type = 'triangle';
    osc2.frequency.setValueAtTime(freq * 2, startTime);

    // 3. Warm acoustic low-pass filter to sound natural and silky smooth
    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(4200, startTime);

    // Natural acoustic envelope: instant attack (4ms), then musical exponential ring
    gain1.gain.setValueAtTime(0.0001, startTime);
    gain1.gain.linearRampToValueAtTime(volume, startTime + 0.004);
    gain1.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

    gain2.gain.setValueAtTime(0.0001, startTime);
    gain2.gain.linearRampToValueAtTime(volume * 0.25, startTime + 0.004);
    gain2.gain.exponentialRampToValueAtTime(0.0001, startTime + duration * 0.60);

    osc1.connect(gain1);
    osc2.connect(gain2);
    gain1.connect(filter);
    gain2.connect(filter);
    filter.connect(dest);

    osc1.start(startTime);
    osc2.start(startTime);
    osc1.stop(startTime + duration + 0.02);
    osc2.stop(startTime + duration + 0.02);
  }

  toggleMute(): boolean {
    this.isMuted = !this.isMuted;
    return this.isMuted;
  }

  getMuted(): boolean {
    return this.isMuted;
  }
}
