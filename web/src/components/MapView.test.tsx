import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MapView, type Layers } from './MapView'
import { rivertown } from '../test/fixture'

const ALL: Layers = { terrain: true, edges: true, minors: true, tokens: true }

describe('MapView', () => {
  it('renders token labels for promoted features', () => {
    render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={ALL} detailedTerrain />)
    expect(screen.getByText('[01]')).toBeInTheDocument()
    expect(screen.getByText('[02]')).toBeInTheDocument()
  })

  it('selects a feature on click', () => {
    const onSelect = vi.fn()
    render(<MapView model={rivertown} selected={null} onSelect={onSelect} layers={ALL} detailedTerrain />)
    fireEvent.click(screen.getByText('[01]'))
    expect(onSelect).toHaveBeenCalledWith('[01]')
  })

  it('hides token labels when the tokens layer is off', () => {
    render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, tokens: false }} detailedTerrain />)
    expect(screen.queryByText('[01]')).not.toBeInTheDocument()
  })

  const minorHitTarget = (c: HTMLElement) =>
    Array.from(c.querySelectorAll('circle')).find(
      (el) => el.getAttribute('r') === '5' && el.getAttribute('fill') === 'transparent',
    )

  it('shows a minor-feature tooltip on hover, without touching token selection', () => {
    const onSelect = vi.fn()
    const { container } = render(<MapView model={rivertown} selected={null} onSelect={onSelect} layers={ALL} detailedTerrain />)
    expect(screen.queryByText('Maple Apartments')).not.toBeInTheDocument() // hidden until hover

    const hit = minorHitTarget(container)!
    fireEvent.mouseEnter(hit)
    expect(screen.getByText('Maple Apartments')).toBeInTheDocument()          // name
    expect(screen.getByText('residential.apartments')).toBeInTheDocument()    // category — "what it is"
    expect(onSelect).not.toHaveBeenCalled()                                   // separate from the token/sidebar path

    fireEvent.mouseLeave(hit)
    expect(screen.queryByText('Maple Apartments')).not.toBeInTheDocument()
  })

  it('has no minor hit targets when the minors layer is off', () => {
    const { container } = render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, minors: false }} detailedTerrain />)
    expect(minorHitTarget(container)).toBeUndefined()
  })
})
