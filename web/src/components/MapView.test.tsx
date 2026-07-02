import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MapView, type Layers } from './MapView'
import { rivertown } from '../test/fixture'

const ALL: Layers = { terrain: true, edges: true, minors: true, props: true, tokens: true }

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

  it('has no minor hit targets when both minor tiers are off', () => {
    const { container } = render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, minors: false, props: false }} detailedTerrain />)
    expect(minorHitTarget(container)).toBeUndefined()
  })

  it('draws named minors and nameless props on separate layers', () => {
    // Only the named minor when props is off; only the prop when minors is off.
    const onlyNamed = render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, props: false }} detailedTerrain />)
    fireEvent.mouseEnter(minorHitTarget(onlyNamed.container)!)
    expect(onlyNamed.getByText('Maple Apartments')).toBeInTheDocument()
    onlyNamed.unmount()

    const onlyProps = render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, minors: false }} detailedTerrain />)
    fireEvent.mouseEnter(minorHitTarget(onlyProps.container)!)
    expect(onlyProps.getByText('house')).toBeInTheDocument() // the nameless prop
  })
})
